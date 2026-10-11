using UnityEngine;

namespace Breathe
{
    /// <summary>Quiet synthesized placeholders; no downloaded audio, voice, or microphone.</summary>
    public sealed class OfficeSound : MonoBehaviour
    {
        AudioSource bed, cues;
        AudioClip warm, dark, click;
        bool horror;
        void Awake()
        {
            bed=gameObject.AddComponent<AudioSource>(); bed.loop=true; bed.playOnAwake=false; bed.volume=.12f;
            cues=gameObject.AddComponent<AudioSource>(); cues.playOnAwake=false; cues.volume=.15f;
            warm=Make("Office hold music",false); dark=Make("After hours",true);
            const int rate=22050; var samples=new float[2205];
            for(int i=0;i<samples.Length;i++) samples[i]=Mathf.Sin(i*2*Mathf.PI*1050/rate)*Mathf.Exp(-i/180f)*.3f;
            click=AudioClip.Create("Pen click",samples.Length,1,rate,false); click.SetData(samples,0);
        }
        AudioClip Make(string label,bool tense)
        {
            const int rate=22050; var data=new float[rate*8];
            float[] notes=tense?new[]{55f,58.27f,82.41f,55f}:new[]{261.63f,329.63f,392f,329.63f};
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate, note=notes[(int)(t*2)%notes.Length];
                float envelope=tense?.55f:Mathf.Exp(-(t*2%1)*5);
                float fade=Mathf.Min(1,Mathf.Min(t*4,(8-t)*4));
                data[i]=Mathf.Sin(t*2*Mathf.PI*note)*envelope*.18f*fade;
            }
            var clip=AudioClip.Create(label,data.Length,1,rate,false); clip.SetData(data,0); return clip;
        }
        public void Set(bool enabled,bool tense,bool paused)
        {
            if(!enabled||paused) { bed.Pause(); return; }
            if(bed.clip==null||horror!=tense) { horror=tense; bed.clip=tense?dark:warm; bed.Play(); }
            else if(!bed.isPlaying) bed.UnPause();
        }
        public void Click(bool enabled) { if(enabled) cues.PlayOneShot(click); }
        void OnDestroy() { Destroy(warm); Destroy(dark); Destroy(click); }
    }
}
