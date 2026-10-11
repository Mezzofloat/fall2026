using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Breathe
{
    [Serializable] public class CameraInfo { public float fps; public int width, height; }
    [Serializable] public class CameraEntry { public string id, name; }
    [Serializable] class CameraList { public CameraEntry[] cameras; }
    [Serializable] class StartPulseRequest { public string camera_id; public bool consent; }
    [Serializable] public class PulseReading
    {
        public CameraInfo camera;
        public int calibration_samples;
        public bool valid,baseline_ready,calibrating,running;
        public float bpm,baseline_bpm,delta_bpm,activation,confidence,age_seconds;
        public string status,message,validation;
    }
    [Serializable] public class GloveSide { public bool connected,click; public int jx,jy; public float activity,fidget; }
    [Serializable] public class ImuReading { public bool connected,calibrating; public float fidget,baseline; public string firmware; public GloveSide left=new GloveSide(),right=new GloveSide(); }
    [Serializable] public class BridgeStatus { public PulseReading presage; public ImuReading imu; public string microphone,voice_id; }
    [Serializable] public class SpeechReply { public string intent,reply,tone,verdict,provider_status; public float anger; public bool fallback; }
    [Serializable] class BridgeConnection { public string url,token; }
    [Serializable] class Transcript { public string text; }
    [Serializable] class VoiceAudio { public string wav_base64; }
    [Serializable] class DialogueRequest { public string act,text,mood; public int candidate,page; }
    [Serializable] class TtsRequest { public string text,tone; }
    [Serializable] class BridgeError { public string error; }

    /// <summary>Asynchronous loopback client. Provider credentials never enter the player.</summary>
    public sealed class LiveServices : MonoBehaviour
    {
        public bool VoiceEnabled {get;private set;}
        public bool BiometricsEnabled {get;private set;}
        public bool Recording {get;private set;}
        public bool Busy {get;private set;}
        public bool Connected {get;private set;}
        public string Status {get;private set;}="Live services off";
        public string MicrophoneName {get;private set;}="";
        public PulseReading Pulse {get;private set;}=new PulseReading();
        public ImuReading Imu {get;private set;}=new ImuReading();
        /// <summary>Loudness of Mr. Vale's voice right now (0..1), for animating his mouth.</summary>
        public float SpeechLevel {get;private set;}
        public float SpeechAnger {get;private set;}
        public string Mood="safe";
        /// <summary>Which follow-up page you hold (0..2), or -1: the bridge judges your answer against that page's question.</summary>
        public int Page=-1;
        public event Action<SpeechReply> Answer;
        public string LastReply {get;private set;}="";
        public bool UsedVoice {get;private set;}
        public bool UsedBiometrics {get;private set;}
        public CameraEntry[] Cameras {get;private set;}=new CameraEntry[0];
        public int CameraIndex {get;private set;}
        public float MicLevel {get;private set;}
        public string SelectedCameraName => Cameras.Length>0?Cameras[Mathf.Clamp(CameraIndex,0,Cameras.Length-1)].name:"(none found)";
        public bool Speaking => speech!=null&&speech.isPlaying;
        BridgeConnection connection;
        AudioSource speech;
        AudioDistortionFilter distortion;
        AudioChorusFilter chorus;
        AudioClip microphoneClip;
        float recordingStart,lastPoll;
        int epoch,speechGeneration;
        Act act;
        int candidate;
        bool suspended,polling;
        string preferredMic="";
        UnityWebRequest answerRequest,ttsRequest;

        void Awake()
        {
            // Vale's voice lives on its own child so the distortion effects never touch the music or the sound effects.
            var voice=new GameObject("Vale voice");voice.transform.SetParent(transform,false);
            speech=voice.AddComponent<AudioSource>();speech.playOnAwake=false;speech.volume=1f;speech.spatialBlend=0f;
            distortion=voice.AddComponent<AudioDistortionFilter>();distortion.distortionLevel=0f;
            chorus=voice.AddComponent<AudioChorusFilter>();chorus.wetMix1=0f;chorus.wetMix2=0f;chorus.wetMix3=0f;chorus.dryMix=1f;chorus.rate=.6f;chorus.depth=.04f;
            LoadConnection();
        }
        void LoadConnection()
        {
            try
            {
                string path=Environment.GetEnvironmentVariable("BREATHE_CONNECTION");
                if(string.IsNullOrEmpty(path)) path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Documents","Codex","breathe-runtime","connection.json");
                if(!File.Exists(path)) return;
                var candidateConnection=JsonUtility.FromJson<BridgeConnection>(File.ReadAllText(path));
                if(Uri.TryCreate(candidateConnection.url,UriKind.Absolute,out var uri)&&uri.Scheme=="http"&&uri.Host=="127.0.0.1"&&uri.AbsolutePath=="/"&&!string.IsNullOrEmpty(candidateConnection.token)) connection=candidateConnection;
            }
            catch { Status="Start the local integration service."; }
        }
        public void Configure(bool voice,bool biometrics)
        {
            VoiceEnabled=voice;BiometricsEnabled=biometrics;
            if(!voice) CancelSpeech();
            if(!biometrics) {Pulse=new PulseReading();StartCoroutine(Command("/presage/stop"));}
            LoadConnection();
            if(voice&&string.IsNullOrEmpty(MicrophoneName)) StartCoroutine(SelectMicrophone());
            if(biometrics&&!suspended) StartCoroutine(Command("/presage/resume"));
        }
        public void SetContext(Act next,int selected,string mood="safe")
        {
            Mood=mood=="horror"?"horror":"safe";
            epoch++;CancelSpeech();act=next;candidate=selected;LastReply="";
            // The tour is the long calm walk: collect the resting baseline there. The camera started in the lobby stays on until the ending.
            if(next==Act.Tour&&BiometricsEnabled) StartCoroutine(Command("/presage/calibrate"));
            if(next==Act.Tour) StartCoroutine(Command("/imu/calibrate"));
            if(next==Act.Ending) {StartCoroutine(Command("/presage/stop"));Pulse=new PulseReading();}
        }
        public void Suspend(bool value)
        {
            if(suspended==value) return;
            suspended=value;epoch++;CancelSpeech();
            if(BiometricsEnabled) StartCoroutine(Command(value?"/presage/pause":"/presage/resume"));
        }
        public void RefreshCameras(){StartCoroutine(LoadCameras());}
        IEnumerator LoadCameras()
        {
            if(connection==null) LoadConnection();
            if(connection==null) {Status="Start the local integration service.";yield break;}
            using(var request=Make("/presage/cameras",null))
            {
                request.timeout=12;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success) {Status=SafeError(request,"Camera discovery failed. Check the Presage diagnostic window.");yield break;}
                try{Cameras=JsonUtility.FromJson<CameraList>(request.downloadHandler.text).cameras??new CameraEntry[0];}catch{Cameras=new CameraEntry[0];}
                // Prefer an external USB webcam over the built-in and Continuity cameras.
                for(int i=0;i<Cameras.Length;i++) if(Cameras[i].name.IndexOf("webcam",StringComparison.OrdinalIgnoreCase)>=0||Cameras[i].name.IndexOf("USB",StringComparison.OrdinalIgnoreCase)>=0){CameraIndex=i;break;}
                Status=Cameras.Length+" camera(s) found";
            }
        }
        /// <summary>Test hook: pretend the bridge reported this glove state.</summary>
        public void DebugSetImu(ImuReading reading){Imu=reading??new ImuReading();}
        public void CycleCamera(){ if(Cameras.Length>0) CameraIndex=(CameraIndex+1)%Cameras.Length; }
        /// <summary>Consent is the player pressing the on-screen toggle. Starts the selected camera on the bridge.</summary>
        public void StartPulse(){StartCoroutine(StartPulseRoutine());}
        IEnumerator StartPulseRoutine()
        {
            if(Cameras.Length==0) {Status="No camera selected. Refresh the camera list.";yield break;}
            BiometricsEnabled=true;
            var payload=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new StartPulseRequest{camera_id=Cameras[CameraIndex].id,consent=true}));
            using(var request=Make("/presage/start",payload))
            {
                request.timeout=20;yield return request.SendWebRequest();
                Status=request.result==UnityWebRequest.Result.Success?"Camera starting · sit centred in steady light":SafeError(request,"Camera could not start. Check the Presage diagnostic window.");
                if(request.result!=UnityWebRequest.Result.Success) BiometricsEnabled=false;
            }
        }
        public void StopPulse(){BiometricsEnabled=false;Pulse=new PulseReading();StartCoroutine(Command("/presage/stop"));}
        public void CycleMicrophone()
        {
            if(Recording||!VoiceEnabled) return;
            var available=Microphone.devices.Where(Suitable).ToArray();
            if(available.Length==0) {Status="No Mac/USB microphone found.";return;}
            int index=Array.IndexOf(available,MicrophoneName);MicrophoneName=available[(index+1)%available.Length];preferredMic=MicrophoneName;
            Status="Microphone: "+MicrophoneName;
        }
        static bool Suitable(string name) => name.IndexOf("airpods",StringComparison.OrdinalIgnoreCase)<0&&name.IndexOf("bluetooth",StringComparison.OrdinalIgnoreCase)<0;
        IEnumerator SelectMicrophone()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if(!VoiceEnabled) yield break;
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone)) {Status="Microphone permission denied. Use keyboard or allow access in System Settings.";yield break;}
            var choices=Microphone.devices.Where(Suitable).ToArray();
            MicrophoneName=choices.FirstOrDefault(d=>!string.IsNullOrEmpty(preferredMic)&&(d==preferredMic||preferredMic.StartsWith(d+" (",StringComparison.Ordinal)))??"";
            if(MicrophoneName==""&&preferredMic=="") MicrophoneName=choices.FirstOrDefault(d=>d.IndexOf("MacBook",StringComparison.OrdinalIgnoreCase)>=0)??choices.FirstOrDefault()??"";
            Status=MicrophoneName==""?"Select an available Mac/USB microphone.":"Ready · hold V to answer";
        }
        void Update()
        {
            // Always watch the bridge (cheap) so the setup clipboard can show connection state; poll slower while it is down.
            if(!polling&&Time.realtimeSinceStartup-lastPoll>(Connected?1f:2.5f)) StartCoroutine(Poll());
            if(Recording&&microphoneClip!=null)
            {
                int position=Microphone.GetPosition(MicrophoneName),window=Mathf.Min(position,1024);
                if(window>0){var buffer=new float[window];microphoneClip.GetData(buffer,position-window);float sum=0;foreach(var v in buffer)sum+=v*v;MicLevel=Mathf.Lerp(MicLevel,Mathf.Clamp01(Mathf.Sqrt(sum/window)*5f),.35f);}
            }
            else MicLevel=Mathf.Lerp(MicLevel,0,.2f);
            UpdateSpeechLevel();
            if(Recording)
            {
                if(!Microphone.devices.Contains(MicrophoneName)) {CancelSpeech();Status="Selected microphone disconnected.";}
                else if(Time.realtimeSinceStartup-recordingStart>=29.5f) EndRecording(true);
            }
        }
        readonly float[] speechWindow=new float[256];
        void UpdateSpeechLevel()
        {
            float level=0;
            if(speech!=null&&speech.isPlaying){speech.GetOutputData(speechWindow,0);float sum=0;foreach(var v in speechWindow)sum+=v*v;level=Mathf.Clamp01(Mathf.Sqrt(sum/speechWindow.Length)*6f);}
            SpeechLevel=Mathf.Lerp(SpeechLevel,level,.5f);
        }
        IEnumerator Poll()
        {
            polling=true;lastPoll=Time.realtimeSinceStartup;
            if(connection==null) LoadConnection();
            if(connection==null) {Connected=false;Status="Start the local integration service.";polling=false;yield break;}
            using(var request=Make("/status",null))
            {
                request.timeout=3;yield return request.SendWebRequest();
                Connected=request.result==UnityWebRequest.Result.Success;
                if(Connected)
                {
                    BridgeStatus state=null;
                    try {state=JsonUtility.FromJson<BridgeStatus>(request.downloadHandler.text);} catch {}
                    Pulse=state?.presage??new PulseReading();Imu=state?.imu??new ImuReading();
                    if(!string.IsNullOrWhiteSpace(state?.microphone)&&preferredMic!=state.microphone)
                    {preferredMic=state.microphone;if(VoiceEnabled&&!Recording) StartCoroutine(SelectMicrophone());}
                    if(BiometricsEnabled&&Pulse.valid&&Pulse.baseline_ready) UsedBiometrics=true;
                }
                else {Pulse=new PulseReading();Imu=new ImuReading();if(!Recording&&!Busy)Status="Bridge disconnected · keyboard controls available";LoadConnection();}
            }
            if(BiometricsEnabled&&!suspended&&act!=Act.Ending) yield return Command("/presage/heartbeat");
            polling=false;
        }
        UnityWebRequest Make(string path,byte[] body,string contentType="application/json")
        {
            var request=new UnityWebRequest(connection.url.TrimEnd('/')+path,body==null?"GET":"POST");
            request.downloadHandler=new DownloadHandlerBuffer();request.timeout=30;request.SetRequestHeader("X-Breathe-Token",connection.token);
            if(body!=null) {request.uploadHandler=new UploadHandlerRaw(body);request.SetRequestHeader("Content-Type",contentType);}
            return request;
        }
        IEnumerator Command(string path)
        {
            if(connection==null) yield break;
            using(var request=Make(path,System.Text.Encoding.UTF8.GetBytes("{}")))
            {request.timeout=4;yield return request.SendWebRequest();}
        }
        public void BeginRecording()
        {
            if(!VoiceEnabled||suspended||Busy||Recording||!LiveRules.CanTalk(act)) return;
            if(!Connected||connection==null) {Status="Local service unavailable. Use the diagnostic window.";return;}
            if(string.IsNullOrEmpty(MicrophoneName)||!Microphone.devices.Contains(MicrophoneName)) {Status="Selected microphone unavailable.";return;}
            speechGeneration++;speech.Stop();ttsRequest?.Abort();
            try {microphoneClip=Microphone.Start(MicrophoneName,false,30,16000);}
            catch {Status="Microphone could not start.";return;}
            if(microphoneClip==null) {Status="Microphone could not start.";return;}
            Recording=true;recordingStart=Time.realtimeSinceStartup;Status="Recording · release V to send";
        }
        public void EndRecording(bool submit)
        {
            if(!Recording) return;
            int count=Microphone.GetPosition(MicrophoneName);Recording=false;Microphone.End(MicrophoneName);
            var clip=microphoneClip;microphoneClip=null;
            if(!submit||clip==null||count<2400) {if(clip)Destroy(clip);Status="Recording stopped";return;}
            var raw=new float[count*clip.channels];clip.GetData(raw,0);var mono=new float[count];
            for(int i=0;i<count;i++) {for(int c=0;c<clip.channels;c++)mono[i]+=raw[i*clip.channels+c];mono[i]/=clip.channels;}
            int rate=clip.frequency;Destroy(clip);
            StartCoroutine(Submit(LiveRules.EncodeWav(mono,rate),epoch,act,candidate,Page));
        }
        IEnumerator Submit(byte[] wav,int version,Act requestedAct,int selected,int page)
        {
            Busy=true;Status="Transcribing…";
            string transcript=null;
            using(var request=Make("/transcribe",wav,"audio/wav"))
            {
                answerRequest=request;yield return request.SendWebRequest();
                if(version!=epoch) yield break;
                if(request.result==UnityWebRequest.Result.Success) {try {transcript=JsonUtility.FromJson<Transcript>(request.downloadHandler.text).text;}catch{}}
                else Status=SafeError(request,"Transcription failed. Check the voice diagnostic window.");
            }
            answerRequest=null;
            if(string.IsNullOrWhiteSpace(transcript)||transcript.Length>1500) {Busy=false;yield break;}
            Status="Interviewer is thinking…";
            var payload=JsonUtility.ToJson(new DialogueRequest{act=requestedAct.ToString(),candidate=selected,text=transcript,mood=Mood,page=page});
            SpeechReply result=null;
            using(var request=Make("/dialogue",System.Text.Encoding.UTF8.GetBytes(payload)))
            {
                answerRequest=request;yield return request.SendWebRequest();
                if(version!=epoch) yield break;
                if(request.result==UnityWebRequest.Result.Success) {try {result=JsonUtility.FromJson<SpeechReply>(request.downloadHandler.text);}catch{}}
                else Status=SafeError(request,"Dialogue unavailable. Use the on-screen choices.");
            }
            answerRequest=null;Busy=false;
            if(result==null||!LiveRules.Allowed(requestedAct,result.intent)||!LiveRules.Speakable(result.reply)) {Status="Invalid reply ignored; keyboard controls remain available.";yield break;}
            UsedVoice=true;LastReply=result.reply;SpeechAnger=Mathf.Clamp01(result.anger);Status=result.fallback?"Scripted reply · Gemini unavailable":"Answer received";
            Answer?.Invoke(result);
        }
        static string SafeError(UnityWebRequest request,string fallback)
        {
            try {var error=JsonUtility.FromJson<BridgeError>(request.downloadHandler.text);if(!string.IsNullOrWhiteSpace(error.error)&&error.error.Length<200)return error.error;}catch{}
            return fallback;
        }
        static readonly string[] Tones={"calm","sarcastic","menacing","furious"};
        public void Say(string line,string tone="calm",float anger=0f)
        {
            if(!VoiceEnabled||suspended||connection==null||string.IsNullOrWhiteSpace(line)) return;
            if(Array.IndexOf(Tones,tone)<0) tone="calm";
            SpeechAnger=Mathf.Clamp01(anger);
            ttsRequest?.Abort();speech.Stop();StartCoroutine(Speak(line,tone,epoch,++speechGeneration));
        }
        IEnumerator Speak(string text,string tone,int version,int ticket)
        {
            var payload=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new TtsRequest{text=text,tone=tone}));
            AudioClip clip=null;
            using(var request=Make("/tts",payload))
            {
                ttsRequest=request;yield return request.SendWebRequest();
                if(version!=epoch||ticket!=speechGeneration||Recording||suspended) yield break;
                if(request.result==UnityWebRequest.Result.Success)
                {
                    try {var data=Convert.FromBase64String(JsonUtility.FromJson<VoiceAudio>(request.downloadHandler.text).wav_base64);var samples=LiveRules.DecodeWav(data,out int rate);LevelVoice(samples);clip=AudioClip.Create("Interviewer voice",samples.Length,1,rate,false);clip.SetData(samples,0);}catch {Status="Invalid speech audio ignored.";}
                }
                else Status=SafeError(request,"Voice playback unavailable; read the subtitles.");
            }
            ttsRequest=null;
            if(clip) {if(speech.clip)Destroy(speech.clip);speech.clip=clip;ApplyVoiceEffects(SpeechAnger);speech.Play();}
        }
        /// <summary>Speech is always the loudest thing in the mix: bring every line to the same RMS so quiet and shouted lines are both clearly audible.</summary>
        public static void LevelVoice(float[] samples,float targetRms=.14f)
        {
            if(samples==null||samples.Length==0) return;
            double sum=0;foreach(var v in samples)sum+=v*v;float rms=Mathf.Sqrt((float)(sum/samples.Length));
            if(rms<1e-4f) return;
            float gain=Mathf.Clamp(targetRms/rms,.5f,12f);
            for(int i=0;i<samples.Length;i++) samples[i]=Mathf.Clamp(samples[i]*gain,-.98f,.98f);
        }
        /// <summary>Anger bends the voice: crunchy distortion, a doubled chorus and a slightly lower pitch.</summary>
        void ApplyVoiceEffects(float anger)
        {
            anger=Mathf.Clamp01(anger);
            distortion.distortionLevel=anger<.3f?0f:Mathf.Lerp(.15f,.72f,(anger-.3f)/.7f);
            float wet=anger<.5f?0f:Mathf.Lerp(.25f,.55f,(anger-.5f)/.5f);
            chorus.wetMix1=wet;chorus.wetMix2=wet*.7f;chorus.wetMix3=wet*.4f;chorus.dryMix=1f-wet*.4f;
            speech.pitch=Mathf.Lerp(1f,.86f,anger);
        }
        public void CancelSpeech()
        {
            epoch++;speechGeneration++;
            EndRecording(false);answerRequest?.Abort();ttsRequest?.Abort();answerRequest=null;ttsRequest=null;Busy=false;
            if(speech) speech.Stop();
        }
        public void OpenDiagnostics(string panel)
        {
            LoadConnection();Application.OpenURL((connection?.url??"http://127.0.0.1:8765").TrimEnd('/')+(panel=="presage"?"/presage":"/voice"));
        }
        void OnDestroy() {epoch++;CancelSpeech();if(speech&&speech.clip)Destroy(speech.clip);}
    }
}
