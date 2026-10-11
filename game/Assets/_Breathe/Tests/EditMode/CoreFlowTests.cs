using NUnit.Framework;
using UnityEngine;
using Breathe;

public class CoreFlowTests
{
    InterviewRun AtFirstChase()
    {
        var run=new InterviewRun(); run.StartSelection(); run.Select(0); run.StartTour();
        for(int i=0;i<4;i++) run.MeetColleague();
        run.Introduce(); run.FinishTransformation(); run.Clarify(); run.FinishUnreadable(); run.StartQuestion();
        return run;
    }
    void CompleteQuestion(InterviewRun run)
    {
        for(int i=0;i<run.Required;i++) { Assert.That(run.Collect(),Is.True); Assert.That(run.Answer(false),Is.True); }
        Assert.That(run.ExitReady,Is.True); run.ExitQuestion();
    }
    InterviewRun AtOffer(bool keep)
    {
        var run=AtFirstChase(); CompleteQuestion(run); run.StartQuestion(); CompleteQuestion(run);
        run.StartQuestion(); CompleteQuestion(run); run.ChooseMemory(keep); run.ReturnToInterview(); return run;
    }
    [Test] public void AllThreeEndingsAreReachable()
    {
        var boundary=AtOffer(true); boundary.Finish(true);
        Assert.That(boundary.EndingTitle,Is.EqualTo("OUT OF OFFICE.")); Assert.That(boundary.Completed,Is.True);
        var retained=AtOffer(true); retained.Finish(false); Assert.That(retained.EndingTitle,Is.EqualTo("YOU KEPT SOMETHING."));
        var erased=AtOffer(false); erased.Finish(false); Assert.That(erased.EndingTitle,Is.EqualTo("WELCOME TO THE TEAM."));
    }
    [Test] public void ExitNeedsEveryPageRecoveredAndAnswered()
    {
        var run=AtFirstChase(); run.ExitQuestion(); Assert.That(run.Act,Is.EqualTo(Act.Challenge));
        Assert.That(run.Collect(),Is.True);
        Assert.That(run.Collect(),Is.False,"The next page needs the previous answer first.");
        Assert.That(run.StationReady,Is.True); Assert.That(run.ExitReady,Is.False);
        Assert.That(run.Answer(true),Is.True); Assert.That(run.Answer(true),Is.False,"One answer per page.");
        Assert.That(run.VoiceAnswers,Is.EqualTo(1));
        for(int i=1;i<3;i++) { Assert.That(run.Collect(),Is.True); Assert.That(run.Answer(false),Is.True); }
        Assert.That(run.Collect(),Is.False,"No more pages than required."); Assert.That(run.ExitReady,Is.True);
        Assert.That(run.AnswerProgress,Is.EqualTo(1f));
    }
    [Test] public void AnswerCannotBeAcceptedOutsideAChase()
    {
        var run=new InterviewRun(); Assert.That(run.Answer(true),Is.False); Assert.That(run.Collect(),Is.False);
    }
    [Test] public void RetryResetsCurrentQuestionAndPreservesCandidate()
    {
        var run=AtFirstChase(); CompleteQuestion(run); run.StartQuestion(); run.Collect(); run.Answer(true);
        var candidate=run.Candidate; run.Catch(); Assert.That(run.Act,Is.EqualTo(Act.Caught)); run.Retry();
        Assert.That(run.Act,Is.EqualTo(Act.Team)); Assert.That(run.Collected,Is.Zero); Assert.That(run.Answered,Is.Zero);
        Assert.That(run.Catches,Is.EqualTo(1)); Assert.That(run.Candidate,Is.SameAs(candidate));
    }
    [Test] public void PeakFearIsBoundedAndIgnoresNaN()
    {
        var run=AtFirstChase(); run.RecordFear(.4f); run.RecordFear(float.NaN); run.RecordFear(float.PositiveInfinity); run.RecordFear(5f); run.RecordFear(-3f);
        Assert.That(run.PeakFear,Is.InRange(.4f,1f)); Assert.That(float.IsNaN(run.PeakFear),Is.False);
        run.Tick(float.NaN); run.Tick(float.PositiveInfinity); run.Tick(-1); Assert.That(float.IsNaN(run.Elapsed),Is.False);
    }
    [Test] public void InvalidActionsCannotSkipStory()
    {
        var run=new InterviewRun(); run.Finish(false); run.ExitQuestion(); run.ChooseMemory(true); run.Introduce();
        Assert.That(run.Act,Is.EqualTo(Act.Welcome)); Assert.That(run.Completed,Is.False);
        Assert.Throws<System.ArgumentOutOfRangeException>(()=>run.Select(-1));
    }
    [Test] public void ClarificationDoesNotSoftLockOrPretendToUnderstandSpeech()
    {
        var run=new InterviewRun(); run.StartSelection(); run.StartTour(); for(int i=0;i<4;i++) run.MeetColleague();
        run.Introduce(); run.FinishTransformation(); for(int i=0;i<10;i++) run.Clarify();
        Assert.That(run.Act,Is.EqualTo(Act.Unreadable)); run.FinishUnreadable(); Assert.That(run.Act,Is.EqualTo(Act.ChallengeIntro));
    }
    [Test] public void EveryObjectiveAndExitIsReachable()
    {
        var map=new OfficeMap();
        foreach(var positions in new[]{map.Tour,map.Challenge,map.Team,map.Gap,map.Shelters,map.Patrol,new[]{map.Exit}})
            foreach(var target in positions) {
                Assert.That(map.Walkable(target),Is.True,"Blocked target "+target);
                Assert.That(map.FindPath(map.Spawn,target).Count,Is.GreaterThan(0),"Unreachable target "+target);
            }
    }
    [Test] public void MovementDoesNotPassThroughWalls()
    {
        var map=new OfficeMap(); var pos=new Vector2(1.5f,5.5f);
        for(int i=0;i<300;i++) pos=map.Move(pos,Vector2.left*.1f);
        Assert.That(pos.x,Is.GreaterThanOrEqualTo(1.24f)); Assert.That(map.Walkable(pos),Is.True);
    }
    [Test] public void LineOfSightIsBlockedByWallsAndClosedDoorsAndClearInTheOpen()
    {
        var map=new OfficeMap();
        Assert.That(map.HasLineOfSight(new Vector2(2.5f,5.5f),new Vector2(8.5f,5.5f)),Is.True);
        Assert.That(map.HasLineOfSight(new Vector2(6.5f,5.5f),new Vector2(6.5f,12.5f)),Is.False,"The wall at y=9 blocks sight.");
        Assert.That(map.HasLineOfSight(new Vector2(4.5f,5.5f),new Vector2(4.5f,10.5f)),Is.False,"A closed door blocks sight.");
        map.Doors.Find(d=>d.Cell==new Vector2Int(4,9)).Open=1f;
        Assert.That(map.HasLineOfSight(new Vector2(4.5f,5.5f),new Vector2(4.5f,10.5f)),Is.True,"An open door lets you see through.");
    }
    [Test] public void FourDistinctFilesHaveRealStoryGaps()
    {
        Assert.That(CandidateFiles.All.Length,Is.EqualTo(4));
        var names=new System.Collections.Generic.HashSet<string>();
        foreach(var c in CandidateFiles.All) { Assert.That(names.Add(c.Name),Is.True); Assert.That(c.Gap.Length,Is.GreaterThan(20)); Assert.That(c.Memory.Length,Is.GreaterThan(20)); }
    }
    [Test] public void SpokenIntentsAreGatedByQuestion()
    {
        Assert.That(LiveRules.Allowed(Act.Name,"answer"),Is.True);
        Assert.That(LiveRules.Allowed(Act.Name,"keep_memory"),Is.False);
        Assert.That(LiveRules.Allowed(Act.GapChoice,"keep_memory"),Is.True);
        Assert.That(LiveRules.Allowed(Act.GapChoice,"answer"),Is.False);
        Assert.That(LiveRules.Allowed(Act.Offer,"unlimited"),Is.True);
        Assert.That(LiveRules.Allowed(Act.Ending,"answer"),Is.False,"Voice can never act outside answerable questions.");
        Assert.That(LiveRules.Allowed(Act.Welcome,"stop"),Is.False);
        Assert.That(LiveRules.Allowed(Act.Challenge,"skip"),Is.False);
        Assert.That(LiveRules.Speakable(new string('x',181)),Is.False);
        Assert.That(LiveRules.Speakable("see http://evil"),Is.False);
        Assert.That(LiveRules.Speakable("Thank you. We can continue."),Is.True);
    }
    [Test] public void WavRoundTripAndHostileWavsAreRejected()
    {
        var samples=new float[2400]; for(int i=0;i<samples.Length;i++) samples[i]=Mathf.Sin(i*.1f)*.5f;
        samples[5]=float.NaN; samples[6]=9f;
        var wav=LiveRules.EncodeWav(samples,16000); var back=LiveRules.DecodeWav(wav,out int rate);
        Assert.That(rate,Is.EqualTo(16000)); Assert.That(back.Length,Is.EqualTo(samples.Length));
        foreach(var bad in new[]{new byte[0],new byte[44],System.Text.Encoding.ASCII.GetBytes("RIFFxxxxWAVEgarbage garbage garbage garbage!")})
            Assert.Throws<System.IO.InvalidDataException>(()=>LiveRules.DecodeWav(bad,out _));
        var truncated=new byte[wav.Length/2]; System.Array.Copy(wav,truncated,truncated.Length);
        Assert.That(()=>LiveRules.DecodeWav(truncated,out _),Throws.Exception);
    }
}

public class UnityContractTests
{
    [Test] public void MagicMethodsNeverTakeParameters()
    {
        // Unity logs "Start() can not take parameters" at runtime but tests do not fail on it, so check by reflection.
        var magic=new System.Collections.Generic.HashSet<string>{"Awake","Start","Update","LateUpdate","FixedUpdate","OnEnable","OnDisable","OnDestroy"};
        foreach(var type in typeof(InterviewRun).Assembly.GetTypes())
        {
            if(!typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
            foreach(var m in type.GetMethods(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly))
                if(magic.Contains(m.Name)) Assert.That(m.GetParameters().Length,Is.Zero,type.Name+"."+m.Name+" would break as a Unity message.");
        }
    }
}

public class FearModelTests
{
    FearModel.Input Pulse(float delta) => new FearModel.Input{PulseValid=true,PulseDeltaBpm=delta};
    void Run(FearModel f,FearModel.Input i,float seconds){ for(float t=0;t<seconds;t+=.1f) f.Update(.1f,i); }
    [Test] public void RestingPulseIsNeitherCalmNorPanicked()
    {
        var f=new FearModel(); Run(f,Pulse(0),30);
        Assert.That(f.Fear,Is.InRange(.1f,.25f)); Assert.That(f.Panic,Is.Zero); Assert.That(f.SpeedScale,Is.EqualTo(1f));
    }
    [Test] public void RacingPulseSlowsMovementButNeverBelowTheFloor()
    {
        var f=new FearModel(); Run(f,Pulse(60),60);
        Assert.That(f.Fear,Is.GreaterThan(.95f)); Assert.That(f.Panic,Is.GreaterThan(.9f));
        Assert.That(f.SpeedScale,Is.EqualTo(f.MinSpeedScale).Within(.05f)); Assert.That(f.SpeedScale,Is.GreaterThanOrEqualTo(f.MinSpeedScale-.001f));
        Assert.That(f.ShakeAmplitude,Is.GreaterThan(0)); Assert.That(f.ShakeAmplitude,Is.LessThan(.05f));
        Assert.That(f.RedPulseHz,Is.LessThanOrEqualTo(f.MaxPulseHz),"The red pulse must stay slow enough to be safe.");
    }
    [Test] public void SpacingOutNeedsSustainedRealCalm()
    {
        var f=new FearModel(); Run(f,Pulse(-10),3); Assert.That(f.SpaceOut,Is.Zero,"Not yet.");
        Run(f,Pulse(-10),15); Assert.That(f.SpaceOut,Is.EqualTo(1f).Within(.01f)); Assert.That(f.LookScale,Is.LessThan(.7f));
        Run(f,Pulse(25),40); Assert.That(f.SpaceOut,Is.Zero,"Calm must reset as soon as the pulse rises.");
    }
    [Test] public void NoSignalIsNeutralAndNeverSpacesOut()
    {
        var f=new FearModel(); Run(f,new FearModel.Input(),60);
        Assert.That(f.Fear,Is.EqualTo(f.NeutralFear).Within(.02f)); Assert.That(f.SpaceOut,Is.Zero); Assert.That(f.Live,Is.False);
    }
    [Test] public void DevOverrideIsNotLiveSoItCannotSpaceOut()
    {
        var f=new FearModel(); Run(f,new FearModel.Input{DevOverride=true,DevFear=0},60);
        Assert.That(f.Fear,Is.LessThan(.05f)); Assert.That(f.Live,Is.False); Assert.That(f.SpaceOut,Is.Zero);
    }
    [Test] public void LoudVoiceRaisesFearAndGarbageInputIsIgnored()
    {
        var f=new FearModel(); var calm=Pulse(0); Run(f,calm,20); float before=f.Fear;
        var loud=calm; loud.VoiceActive=true; loud.VoiceLoudness=1f; Run(f,loud,20); Assert.That(f.Fear,Is.GreaterThan(before+.05f));
        foreach(var bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        { f.Update(.1f,new FearModel.Input{PulseValid=true,PulseDeltaBpm=bad,VoiceActive=true,VoiceLoudness=bad}); }
        f.Update(float.NaN,Pulse(0)); f.Update(-1,Pulse(0)); f.Update(float.PositiveInfinity,Pulse(0));
        Assert.That(float.IsNaN(f.Fear),Is.False); Assert.That(f.Fear,Is.InRange(0f,1f));
    }
    [Test] public void FearChangesSmoothlyBetweenSparseReadings()
    {
        var f=new FearModel(); Run(f,Pulse(0),20); float before=f.Fear; f.Update(.1f,Pulse(40));
        Assert.That(f.Fear-before,Is.LessThan(.05f),"One frame must not jump to the new value.");
    }
}

public class HunterAITests
{
    [Test] public void IdleHunterPatrolsAndStaysOnWalkableCells()
    {
        var map=new OfficeMap(); var ai=new HunterAI(map,map.HunterSpawn,1,0f); var start=ai.Position; float travelled=0; var last=start;
        for(int i=0;i<3600;i++) { map.UpdateDoors(.05f,new Vector2(2.5f,5.5f),ai.Position); ai.Tick(.05f,new Vector2(2.5f,5.5f),true,0f); travelled+=Vector2.Distance(last,ai.Position); last=ai.Position; Assert.That(map.Walkable(ai.Position),Is.True); }
        Assert.That(travelled,Is.GreaterThan(30f),"He must actually walk around, through doors.");
    }
    [Test] public void SeesAPlayerInTheOpenAndChasesAndCatchesAnIdleOne()
    {
        var map=new OfficeMap(); var player=new Vector2(22.5f,3.5f); var ai=new HunterAI(map,new Vector2(27.5f,3.5f),2,0f);
        bool caught=false; for(int i=0;i<2400&&!caught;i++) { map.UpdateDoors(.05f,player,ai.Position); caught=ai.Tick(.05f,player,false,1f); }
        Assert.That(caught,Is.True,"An idle, visible player must eventually be caught.");
    }
    [Test] public void HeadStartPreventsAnImmediateCatch()
    {
        var map=new OfficeMap(); var ai=new HunterAI(map,new Vector2(5.5f,5.5f),3,5f);
        for(int i=0;i<90;i++) Assert.That(ai.Tick(.05f,new Vector2(5.9f,5.5f),false,5f),Is.False);
    }
    [Test] public void LoudNoiseDrawsHimButQuietHidingDoesNot()
    {
        var map=new OfficeMap(); var player=map.Shelters[0];
        var quiet=new HunterAI(map,new Vector2(15.5f,5.5f),4,0f); var loud=new HunterAI(map,new Vector2(15.5f,5.5f),4,0f);
        float quietMin=999,loudMin=999;
        for(int i=0;i<800;i++) { map.UpdateDoors(.05f,player,quiet.Position); quiet.Tick(.05f,player,true,.3f); loud.Tick(.05f,player,true,20f); quietMin=Mathf.Min(quietMin,Vector2.Distance(quiet.Position,player)); loudMin=Mathf.Min(loudMin,Vector2.Distance(loud.Position,player)); }
        Assert.That(loudMin,Is.LessThan(quietMin+.01f));
    }
    [Test] public void BadTimeStepsNeverCorruptPosition()
    {
        var map=new OfficeMap(); var ai=new HunterAI(map,map.HunterSpawn,5,0f);
        foreach(var dt in new[]{float.NaN,float.PositiveInfinity,-1f,0f,1000f}) ai.Tick(dt,new Vector2(3.5f,3.5f),false,5f);
        Assert.That(float.IsNaN(ai.Position.x),Is.False); Assert.That(map.Walkable(ai.Position),Is.True);
    }
}

public class DoorTests
{
    static Door At(OfficeMap map,int x,int y)=>map.Doors.Find(d=>d.Cell==new Vector2Int(x,y));
    [Test] public void BuildingIsThreeByThreeRoomsJoinedByTwelveDoors()
    {
        var map=new OfficeMap(); Assert.That(map.Doors.Count,Is.EqualTo(12)); Assert.That(OfficeMap.RoomNames.Length,Is.EqualTo(9));
        var seen=new System.Collections.Generic.HashSet<int>();
        foreach(var p in map.Patrol) { seen.Add(OfficeMap.RoomIndex(p)); Assert.That(map.FindPath(map.Spawn,p).Count,Is.GreaterThan(0)); }
        Assert.That(seen.Count,Is.EqualTo(9),"Every room is reachable and has a patrol stop.");
        var kinds=new System.Collections.Generic.HashSet<int>(); foreach(var b in map.Blocks) kinds.Add(b.Room); Assert.That(kinds.Count,Is.EqualTo(9),"Every room has furniture.");
    }
    [Test] public void ClosedDoorsBlockWalkingAndOpenDoorsDoNot()
    {
        var map=new OfficeMap(); var door=At(map,10,4); var before=new Vector2(8.5f,4.5f);
        Assert.That(map.Solid(10,4),Is.True); var after=before; for(int i=0;i<60;i++) after=map.Move(after,Vector2.right*.05f); Assert.That(after.x,Is.LessThan(10f));
        door.Open=1f; Assert.That(map.Solid(10,4),Is.False); for(int i=0;i<60;i++) after=map.Move(after,Vector2.right*.05f); Assert.That(after.x,Is.GreaterThan(10.5f));
    }
    [Test] public void PlayerOpensDoorsJustByWalkingUpAndTheyCloseBehindAfterAWhile()
    {
        var map=new OfficeMap(); var door=At(map,10,4); var far=new Vector2(2.5f,5.5f); var near=new Vector2(9.5f,4.5f);
        for(int i=0;i<20;i++) map.UpdateDoors(.05f,near,far); Assert.That(door.Open,Is.EqualTo(1f).Within(.01f));
        Assert.That(map.Events.Count,Is.GreaterThan(0)); Assert.That(map.Events.Dequeue().kind,Is.EqualTo(DoorEvent.Opened));
        for(int i=0;i<60;i++) map.UpdateDoors(.1f,new Vector2(5.5f,5.5f),far); Assert.That(door.Open,Is.EqualTo(0f).Within(.01f),"Doors close when nobody is near.");
    }
    [Test] public void MrValeWaitsAboutOneSecondAtAClosedDoor()
    {
        var map=new OfficeMap(); var ai=new HunterAI(map,new Vector2(8.5f,4.5f),9,0f); var player=new Vector2(14.5f,4.5f);
        float waited=0; bool opened=false;
        for(int i=0;i<200&&!opened;i++)
        {
            var before=ai.Position; ai.Tick(.05f,player,false,0f,1f,false);
            if(ai.WaitingAtDoor) waited+=.05f; if(ai.DoorsOpened>0) opened=true;
        }
        Assert.That(opened,Is.True); Assert.That(waited,Is.InRange(.8f,1.15f),"He loses about a second at a door when you are calm.");
        Assert.That(map.Events.Count,Is.GreaterThan(0)); Assert.That(map.Events.Peek().kind,Is.EqualTo(DoorEvent.Opened));
    }
    [Test] public void WhenStressIsElevatedHeBurstsThroughInstantly()
    {
        var map=new OfficeMap(); var ai=new HunterAI(map,new Vector2(8.5f,4.5f),9,0f); var player=new Vector2(14.5f,4.5f);
        float waited=0;
        for(int i=0;i<200&&ai.DoorsBurst==0;i++) { ai.Tick(.05f,player,false,0f,1f,true); if(ai.WaitingAtDoor) waited+=.05f; }
        Assert.That(ai.DoorsBurst,Is.EqualTo(1)); Assert.That(ai.DoorsOpened,Is.Zero); Assert.That(waited,Is.LessThan(.12f),"No waiting when you are stressed.");
        Assert.That(map.Events.Peek().kind,Is.EqualTo(DoorEvent.Burst));
    }
    [Test] public void ClosedDoorsHideThePlayerFromTheHunter()
    {
        var map=new OfficeMap(); var ai=new HunterAI(map,new Vector2(4.5f,7.5f),1,0f);
        Assert.That(ai.Sees(new Vector2(4.5f,10.5f),false),Is.False,"A closed door blocks sight.");
        At(map,4,9).Open=1f; Assert.That(ai.Sees(new Vector2(4.5f,10.5f),false),Is.True);
    }
    [Test] public void MiniMapDrawsWallsDoorsAndAlcoves()
    {
        var map=new OfficeMap(); var px=map.MiniMapStatic(out int w,out int h);
        Assert.That(w,Is.EqualTo(OfficeMap.Width*OfficeMap.MiniScale)); Assert.That(h,Is.EqualTo(OfficeMap.Height*OfficeMap.MiniScale)); Assert.That(px.Length,Is.EqualTo(w*h));
        int doorPixels=0; foreach(var p in px) if(p.r==240&&p.g==190&&p.b==70) doorPixels++;
        Assert.That(doorPixels,Is.EqualTo(12*OfficeMap.MiniScale*OfficeMap.MiniScale));
    }
}

public class StressFusionTests
{
    void Run(FearModel f,FearModel.Input i,float seconds){ for(float t=0;t<seconds;t+=.1f) f.Update(.1f,i); }
    [Test] public void FidgetingRaisesStressEvenWithoutAPulse()
    {
        var f=new FearModel(); Run(f,new FearModel.Input{FidgetValid=true,Fidget=0},30); float calm=f.Fear;
        Run(f,new FearModel.Input{FidgetValid=true,Fidget=1},30);
        Assert.That(f.Source,Is.EqualTo(FearModel.SourceKind.Glove)); Assert.That(f.Fear,Is.GreaterThan(calm+.6f)); Assert.That(f.Live,Is.True);
    }
    [Test] public void PulseAndGloveAreBlendedNeitherDecidesAlone()
    {
        var f=new FearModel(); Run(f,new FearModel.Input{PulseValid=true,PulseDeltaBpm=40,FidgetValid=true,Fidget=0},40);
        Assert.That(f.Source,Is.EqualTo(FearModel.SourceKind.PulseAndGlove));
        Assert.That(f.Fear,Is.InRange(.5f,.7f),"A racing pulse with a still hand is a middling stress score.");
        var g=new FearModel(); Run(g,new FearModel.Input{PulseValid=true,PulseDeltaBpm=40,FidgetValid=true,Fidget=1},40);
        Assert.That(g.Fear,Is.GreaterThan(.9f));
        var h=new FearModel(); Run(h,new FearModel.Input{PulseValid=true,PulseDeltaBpm=-10,FidgetValid=true,Fidget=1},40);
        Assert.That(h.Fear,Is.InRange(.3f,.6f),"A calm pulse with a restless hand is not full panic.");
    }
    [Test] public void ElevatedStressIsWhatMakesDoorsFallOpen()
    {
        var f=new FearModel(); Assert.That(f.Elevated,Is.False);
        Run(f,new FearModel.Input{PulseValid=true,PulseDeltaBpm=30,FidgetValid=true,Fidget=1},40); Assert.That(f.Elevated,Is.True);
        Run(f,new FearModel.Input{PulseValid=true,PulseDeltaBpm=-10,FidgetValid=true,Fidget=0},60); Assert.That(f.Elevated,Is.False);
    }
    [Test] public void ComponentsAreExposedAndBounded()
    {
        var f=new FearModel(); Run(f,new FearModel.Input{PulseValid=true,PulseDeltaBpm=1000,FidgetValid=true,Fidget=50,VoiceActive=true,VoiceLoudness=9},10);
        Assert.That(f.PulseComponent,Is.InRange(0f,1f)); Assert.That(f.GloveComponent,Is.InRange(0f,1f)); Assert.That(f.VoiceComponent,Is.InRange(0f,1f)); Assert.That(f.Fear,Is.InRange(0f,1f));
        f.Update(.1f,new FearModel.Input{FidgetValid=true,Fidget=float.NaN}); Assert.That(float.IsNaN(f.Fear),Is.False);
    }
}

public class GloveTests
{
    static ImuReading Imu(int ljx,int ljy,bool lclick,int rjx,int rjy,bool rclick,bool l=true,bool r=true)=>new ImuReading{connected=l||r,left=new GloveSide{connected=l,jx=ljx,jy=ljy,click=lclick},right=new GloveSide{connected=r,jx=rjx,jy=rjy,click=rclick}};
    [Test] public void DeadzoneIsIgnoredAndShapeIsMonotonic()
    {
        Assert.That(GloveInput.Shape(5),Is.Zero); Assert.That(GloveInput.Shape(-10),Is.Zero); Assert.That(GloveInput.Shape(100),Is.EqualTo(1f).Within(.001f)); Assert.That(GloveInput.Shape(-100),Is.EqualTo(-1f).Within(.001f));
        float last=0; for(int p=15;p<=100;p+=5) { float v=GloveInput.Shape(p); Assert.That(v,Is.GreaterThan(last)); last=v; }
        Assert.That(GloveInput.Shape(float.NaN),Is.Zero); Assert.That(GloveInput.Shape(5000),Is.EqualTo(1f).Within(.001f));
    }
    [Test] public void LeftStickWalksRightStickLooksClicksAreEandV()
    {
        var g=new GloveInput(); g.Update(Imu(0,100,false,60,-60,false));
        Assert.That(g.Move.y,Is.GreaterThan(.9f)); Assert.That(g.Look.x,Is.GreaterThan(.1f)); Assert.That(g.Look.y,Is.LessThan(-.1f)); Assert.That(g.Sprint,Is.True);
        g.Update(Imu(0,0,true,0,0,false)); Assert.That(g.InteractPressed,Is.True); Assert.That(g.TalkHeld,Is.False);
        g.Update(Imu(0,0,true,0,0,false)); Assert.That(g.InteractPressed,Is.False,"A held click is one press."); Assert.That(g.InteractHeld,Is.True);
        g.Update(Imu(0,0,false,0,0,true)); Assert.That(g.TalkPressed,Is.True); Assert.That(g.TalkHeld,Is.True);
        g.Update(Imu(0,0,false,0,0,true)); Assert.That(g.TalkPressed,Is.False);
        g.Update(Imu(0,0,false,0,0,false)); Assert.That(g.TalkReleased,Is.True); Assert.That(g.TalkHeld,Is.False);
    }
    [Test] public void AGloveThatDisconnectsReleasesEverythingAtOnce()
    {
        var g=new GloveInput(); g.Update(Imu(0,100,true,0,0,true)); Assert.That(g.TalkHeld,Is.True);
        g.Update(Imu(0,100,true,0,0,true,l:false,r:false)); Assert.That(g.Move,Is.EqualTo(Vector2.zero)); Assert.That(g.TalkHeld,Is.False); Assert.That(g.TalkReleased,Is.True); Assert.That(g.Active,Is.False);
        g.Update(null); Assert.That(g.Active,Is.False);
    }
    [Test] public void InvertAndSwapAreAvailableForWiring()
    {
        var g=new GloveInput{InvertLeftY=true}; g.Update(Imu(0,100,false,0,0,false)); Assert.That(g.Move.y,Is.LessThan(-.9f));
        var s=new GloveInput{SwapSticks=true}; s.Update(Imu(0,100,false,0,0,false)); Assert.That(s.Look.y,Is.GreaterThan(.9f));
    }
    [Test] public void FidgetIsOnlyUsedOnceCalibrated()
    {
        var g=new GloveInput(); var imu=Imu(0,0,false,0,0,false); imu.fidget=.7f; imu.calibrating=true; g.Update(imu); Assert.That(g.FidgetValid,Is.False);
        imu.calibrating=false; g.Update(imu); Assert.That(g.FidgetValid,Is.True); Assert.That(g.Fidget,Is.EqualTo(.7f).Within(.001f));
    }
}

public class DialogueRuleTests
{
    [Test] public void ChatIsAllowedWhereverValeExistsAndNeverInMenus()
    {
        foreach(var act in new[]{Act.Tour,Act.Name,Act.Transformation,Act.Unreadable,Act.ChallengeIntro,Act.Challenge,Act.TeamIntro,Act.Team,Act.GapIntro,Act.Gap,Act.GapChoice,Act.Return,Act.Offer,Act.Caught})
        { Assert.That(LiveRules.CanTalk(act),Is.True,act.ToString()); Assert.That(LiveRules.Allowed(act,"chat"),Is.True,act.ToString()); }
        foreach(var act in new[]{Act.Welcome,Act.Resume,Act.Ending}) { Assert.That(LiveRules.CanTalk(act),Is.False); Assert.That(LiveRules.Allowed(act,"chat"),Is.False); }
    }
    [Test] public void ChatDoesNotSmuggleInConsequentialIntents()
    {
        Assert.That(LiveRules.Allowed(Act.Challenge,"keep_memory"),Is.False); Assert.That(LiveRules.Allowed(Act.Tour,"answer"),Is.False); Assert.That(LiveRules.Allowed(Act.Name,"skip"),Is.False);
    }
    [Test] public void VoiceIsLeveledSoItIsAlwaysAudibleAndNeverClips()
    {
        var quiet=new float[4800]; for(int i=0;i<quiet.Length;i++) quiet[i]=Mathf.Sin(i*.1f)*.02f; LiveServices.LevelVoice(quiet);
        double sum=0; foreach(var v in quiet) sum+=v*v; Assert.That(Mathf.Sqrt((float)(sum/quiet.Length)),Is.EqualTo(.14f).Within(.03f));
        var loud=new float[4800]; for(int i=0;i<loud.Length;i++) loud[i]=Mathf.Sin(i*.1f)*.99f; LiveServices.LevelVoice(loud);
        foreach(var v in loud) Assert.That(Mathf.Abs(v),Is.LessThanOrEqualTo(.98f));
        var silence=new float[100]; LiveServices.LevelVoice(silence); foreach(var v in silence) Assert.That(v,Is.Zero);
        LiveServices.LevelVoice(null); LiveServices.LevelVoice(new float[0]);
    }
}

public class RoundFlowTests
{
    [Test] public void HorrorOnlyHappensWhileAQuestionIsAskedOrChased()
    {
        var run=new InterviewRun(); run.StartSelection(); run.StartTour();
        Assert.That(run.IsHorror,Is.False); for(int i=0;i<4;i++) run.MeetColleague(); Assert.That(run.Act,Is.EqualTo(Act.Name)); Assert.That(run.IsHorror,Is.False,"The first question is the safe office.");
        run.Introduce(); Assert.That(run.IsHorror,Is.True); run.FinishTransformation(); Assert.That(run.Act,Is.EqualTo(Act.Unreadable)); Assert.That(run.IsHorror,Is.True);
        run.FinishUnreadable(); Assert.That(run.Act,Is.EqualTo(Act.ChallengeIntro)); Assert.That(run.IsHorror,Is.False,"Between rounds is safe.");
        run.StartQuestion(); Assert.That(run.IsHorror,Is.True,"Asking the next question brings horror back.");
        for(int i=0;i<3;i++) { run.Collect(); run.Answer(false); } run.ExitQuestion(); Assert.That(run.Act,Is.EqualTo(Act.TeamIntro)); Assert.That(run.IsHorror,Is.False);
        run.StartQuestion(); Assert.That(run.IsHorror,Is.True); run.Catch(); Assert.That(run.Act,Is.EqualTo(Act.Caught)); Assert.That(run.IsHorror,Is.True); run.Retry(); Assert.That(run.IsHorror,Is.True);
        for(int i=0;i<2;i++) { run.Collect(); run.Answer(false); } run.ExitQuestion(); Assert.That(run.IsHorror,Is.False);
        run.StartQuestion(); run.Collect(); run.Answer(false); run.ExitQuestion(); Assert.That(run.Act,Is.EqualTo(Act.GapChoice)); Assert.That(run.IsHorror,Is.True);
        run.ChooseMemory(true); Assert.That(run.IsHorror,Is.False); run.ReturnToInterview(); Assert.That(run.IsHorror,Is.False); run.Finish(true); Assert.That(run.IsHorror,Is.False);
    }
    [Test] public void QuestionNumberAndPageStatusAreAlwaysClear()
    {
        var run=new InterviewRun(); run.StartSelection(); run.StartTour(); for(int i=0;i<4;i++) run.MeetColleague();
        Assert.That(run.QuestionNumber,Is.EqualTo(1)); run.Introduce(); Assert.That(run.QuestionNumber,Is.EqualTo(2)); run.FinishTransformation(); run.FinishUnreadable(); Assert.That(run.QuestionNumber,Is.EqualTo(3));
        run.StartQuestion(); Assert.That(run.QuestionNumber,Is.EqualTo(3));
        Assert.That(run.PageStateOf(0),Is.EqualTo(PageState.Find)); Assert.That(run.PageStateOf(1),Is.EqualTo(PageState.Locked));
        run.Collect(); Assert.That(run.PageStateOf(0),Is.EqualTo(PageState.Answer)); Assert.That(run.PageStateOf(1),Is.EqualTo(PageState.Locked));
        run.RejectAnswer(); Assert.That(run.RejectedAnswers,Is.EqualTo(1)); Assert.That(run.Answered,Is.Zero,"A rejected answer does not count.");
        run.Answer(true); Assert.That(run.PageStateOf(0),Is.EqualTo(PageState.Done)); Assert.That(run.PageStateOf(1),Is.EqualTo(PageState.Find));
        Assert.That(run.PageStateOf(-1),Is.EqualTo(PageState.Locked)); Assert.That(run.PageStateOf(9),Is.EqualTo(PageState.Locked));
        run.Catch(); Assert.That(run.QuestionNumber,Is.EqualTo(3),"A retry keeps its question number.");
    }
}


public class FollowUpAndMenuTests
{
    [Test] public void EveryPageHasItsOwnFollowUpQuestion()
    {
        foreach(var (act,count) in new[]{(Act.Challenge,3),(Act.Team,2),(Act.Gap,1)})
        {
            var seen=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<count;i++) { var q=InterviewRun.PageQuestion(act,i); Assert.That(q.Length,Is.GreaterThan(20),act+" page "+i); Assert.That(seen.Add(q),Is.True,"Each page asks something different."); }
            Assert.That(InterviewRun.PageQuestion(act,count),Is.Empty); Assert.That(InterviewRun.PageQuestion(act,-1),Is.Empty);
        }
        Assert.That(InterviewRun.PageQuestion(Act.Name,0),Is.Empty);
    }
    [Test] public void TheHeldPagesQuestionIsAvailableAnywhereUntilAnswered()
    {
        var run=new InterviewRun(); run.StartSelection(); run.StartTour(); for(int i=0;i<4;i++) run.MeetColleague(); run.Introduce(); run.FinishTransformation(); run.FinishUnreadable(); run.StartQuestion();
        Assert.That(run.CurrentPageQuestion,Is.Empty); run.Collect(); Assert.That(run.CurrentPageQuestion,Does.Contain("situation"));
        run.Answer(true); Assert.That(run.CurrentPageQuestion,Is.Empty); run.Collect(); Assert.That(run.CurrentPageQuestion,Does.Contain("do about it"));
    }
    [Test] public void MenusShowNoStressEffects()
    {
        var f=new FearModel{Muted=true}; for(float t=0;t<60;t+=.1f) f.Update(.1f,new FearModel.Input{PulseValid=true,PulseDeltaBpm=60});
        Assert.That(f.Fear,Is.GreaterThan(.9f)); Assert.That(f.Panic,Is.Zero); Assert.That(f.SpaceOut,Is.Zero); Assert.That(f.ShakeAmplitude,Is.Zero); Assert.That(f.RedPulseDepth,Is.Zero);
        f.Muted=false; Assert.That(f.Panic,Is.GreaterThan(.5f));
        var calm=new FearModel{Muted=true}; for(float t=0;t<40;t+=.1f) calm.Update(.1f,new FearModel.Input{PulseValid=true,PulseDeltaBpm=-10}); Assert.That(calm.SpaceOut,Is.Zero,"No blur in the menu.");
    }
}
