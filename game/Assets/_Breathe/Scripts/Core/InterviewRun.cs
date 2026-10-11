using System;
using System.Collections.Generic;

namespace Breathe
{
    public enum PageState { Locked, Find, Answer, Done }

    public enum Act { Welcome, Resume, Tour, Name, Transformation, Unreadable, ChallengeIntro, Challenge, TeamIntro, Team, GapIntro, Gap, GapChoice, Return, Offer, Ending, Caught }

    public sealed class Candidate
    {
        public string Name, Role, Initials, Experience, Challenge, Team, Gap, Memory;
        public Candidate(string name, string role, string initials, string experience, string challenge, string team, string gap, string memory)
        { Name = name; Role = role; Initials = initials; Experience = experience; Challenge = challenge; Team = team; Gap = gap; Memory = memory; }
    }

    public static class CandidateFiles
    {
        public static readonly Candidate[] All = {
            new Candidate("MARA VOSS", "Junior systems engineer", "MV", "2024-26 / Campus IT\nBuilt a student equipment checkout tool.",
                "Recovered a failed demo by isolating the broken service and running a smaller working version.",
                "Shared the repair work with two classmates; documented the fix so the next team could use it.",
                "JUN-NOV 2023 / Took time away to care for her father.", "My father learned to walk again. I was there."),
            new Candidate("ELI PARK", "Experience designer", "EP", "2024-26 / Community design studio\nRedesigned a neighborhood library website.",
                "Rebuilt an inaccessible booking form after testing it with actual library visitors.",
                "Worked with librarians and a developer; changed the design when their feedback contradicted his assumptions.",
                "JAN-AUG 2023 / Helped rebuild the family home after a flood.", "We painted the kitchen yellow. It felt like ours again."),
            new Candidate("NOOR PATEL", "Quality analyst", "NP", "2024-26 / Student software lab\nTested the campus shuttle application.",
                "Found an intermittent timing bug, made a repeatable test, and verified the team's repair.",
                "Paired with a new developer and wrote clear bug reports instead of assigning blame.",
                "SEP 2022-MAY 2023 / Changed fields and studied independently.", "I was allowed to start over. I still am."),
            new Candidate("JONAH REED", "Operations associate", "JR", "2024-26 / Neighborhood food bank\nCoordinated volunteers and weekly deliveries.",
                "Rerouted a canceled delivery and contacted every recipient before closing time.",
                "Asked volunteers what they could realistically take on and shared the remaining route.",
                "MAR-DEC 2023 / Traveled with his sister before she moved abroad.", "For once, we had nowhere we needed to be.")
        };
    }

    /// <summary>Offline story state. No physiological or speech claims are made here.</summary>
    public sealed class InterviewRun
    {
        public Act Act { get; private set; } = Act.Welcome;
        public Candidate Candidate { get; private set; } = CandidateFiles.All[0];
        public int TourStop { get; private set; }
        public int Collected { get; private set; }
        public int Catches { get; private set; }
        public int Clarifications { get; private set; }
        /// <summary>Stations whose spoken (or keyboard-fallback) answer has been accepted.</summary>
        public int Answered { get; private set; }
        public int VoiceAnswers { get; private set; }
        public int KeyboardAnswers { get; private set; }
        public float PeakFear { get; private set; }
        public float AnswerProgress => Required == 0 ? 0f : (float)Answered / Required;
        public bool KeptMemory { get; private set; } = true;
        public bool SetBoundary { get; private set; }
        public bool Completed { get; private set; }
        public bool Sound = true;
        public float Elapsed { get; private set; }
        Act checkpoint;
        public readonly List<string> Record = new List<string>();
        public bool IsChase => Act == Act.Challenge || Act == Act.Team || Act == Act.Gap;
        public bool IsMap => IsChase || Act == Act.Tour;
        public int Required => Act == Act.Challenge ? 3 : Act == Act.Team ? 2 : Act == Act.Gap ? 1 : 0;
        public bool ExitReady => IsChase && Answered >= Required;
        /// <summary>A page has been recovered and now needs its answer before the next one can be taken.</summary>
        public bool StationReady => IsChase && Collected > Answered;
        /// <summary>Horror only while a question is being asked or chased. Between rounds the world returns to the safe office.</summary>
        public bool IsHorror => Act == Act.Transformation || Act == Act.Unreadable || IsChase || Act == Act.GapChoice || Act == Act.Caught;
        public const int QuestionTotal = 5;
        /// <summary>1..5 for the five interview questions, 6 for the last one, 0 outside the interview.</summary>
        public int QuestionNumber => Act == Act.Name ? 1 : Act == Act.Transformation || Act == Act.Unreadable ? 2 : Act == Act.ChallengeIntro || Act == Act.Challenge ? 3
            : Act == Act.TeamIntro || Act == Act.Team ? 4 : Act == Act.GapIntro || Act == Act.Gap || Act == Act.GapChoice ? 5 : Act == Act.Offer || Act == Act.Return ? 6 : Caught() ? checkpointNumber : 0;
        bool Caught() => Act == Act.Caught;
        int checkpointNumber => checkpoint == Act.Challenge ? 3 : checkpoint == Act.Team ? 4 : checkpoint == Act.Gap ? 5 : 0;
        public int RejectedAnswers { get; private set; }
        static readonly string[] ChallengePages = { "SITUATION: Describe the situation. What was the challenge, and what was at stake?", "ACTION: What exactly did you do about it, and why did you choose that?", "RESULT: What was the result, and what did you learn from it?" };
        static readonly string[] TeamPages = { "Why did you ask for help, and who did you ask?", "How did you and your team share the work, and how did it turn out?" };
        static readonly string[] GapPages = { "What did you do during those months, and why is it worth keeping on your record?" };
        /// <summary>The follow-up question printed on page i of a chase question (the bridge has the same list, keyed by page number).</summary>
        public static string PageQuestion(Act act, int index)
        {
            var list = act == Act.Challenge ? ChallengePages : act == Act.Team ? TeamPages : act == Act.Gap ? GapPages : null;
            return list != null && index >= 0 && index < list.Length ? list[index] : "";
        }
        /// <summary>The follow-up for the page you are holding right now ("" when you have no page waiting for an answer).</summary>
        public string CurrentPageQuestion => StationReady ? PageQuestion(Act, Collected - 1) : "";
        /// <summary>State of page i of the current question: locked until reached, then find, answer, done.</summary>
        public PageState PageStateOf(int index)
        {
            if (!IsChase || index < 0 || index >= Required) return PageState.Locked;
            if (index < Answered) return PageState.Done;
            if (index == Collected - 1 && StationReady) return PageState.Answer;
            if (index == Collected && !StationReady) return PageState.Find;
            return PageState.Locked;
        }
        /// <summary>An answer was heard but did not count (off topic or empty).</summary>
        public void RejectAnswer() { if (StationReady) RejectedAnswers++; }

        public void Select(int index)
        {
            if (index < 0 || index >= CandidateFiles.All.Length) throw new ArgumentOutOfRangeException(nameof(index));
            Candidate = CandidateFiles.All[index];
        }
        public void StartSelection() { if (Act == Act.Welcome) Act = Act.Resume; }
        public void StartTour() { if (Act == Act.Resume) { Act = Act.Tour; TourStop = 0; } }
        public void MeetColleague()
        {
            if (Act != Act.Tour) return;
            TourStop++;
            if (TourStop >= 4) Act = Act.Name;
        }
        public void Introduce() { if (Act == Act.Name) { Act = Act.Transformation; Record.Add("You introduced yourself as " + Candidate.Name + "."); } }
        public void FinishTransformation() { if (Act == Act.Transformation) Act = Act.Unreadable; }
        public void Clarify() { if (Act == Act.Unreadable) Clarifications++; }
        public void FinishUnreadable()
        {
            if (Act != Act.Unreadable) return;
            Record.Add(Clarifications > 0 ? "You asked for clarification when the question was unclear." : "You answered an unclear question without asking for clarification.");
            Act = Act.ChallengeIntro;
        }
        public void StartQuestion()
        {
            if (Act == Act.ChallengeIntro) Act = Act.Challenge;
            else if (Act == Act.TeamIntro) Act = Act.Team;
            else if (Act == Act.GapIntro) Act = Act.Gap;
            else return;
            checkpoint = Act;
            ResetQuestion();
        }
        void ResetQuestion() { Collected = 0; Answered = 0; }
        public void RecordFear(float fear) { if (!float.IsNaN(fear) && !float.IsInfinity(fear)) PeakFear = Math.Max(PeakFear, Math.Min(1f, Math.Max(0f, fear))); }
        /// <summary>Recover the next page. Only possible once the previous page's answer was accepted.</summary>
        public bool Collect()
        {
            if (!IsChase || Collected >= Required || Collected > Answered) return false;
            Collected++; return true;
        }
        /// <summary>Accept the answer for the page that was just recovered.</summary>
        public bool Answer(bool spoken)
        {
            if (!StationReady) return false;
            Answered++; if (spoken) VoiceAnswers++; else KeyboardAnswers++; return true;
        }
        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            seconds = Math.Min(seconds, .1f); // Ignore stalls and out-of-focus catch-up.
            if (IsMap) Elapsed += seconds;
        }
        public void ExitQuestion()
        {
            if (!ExitReady) return;
            if (Act == Act.Challenge) { Act = Act.TeamIntro; Record.Add("You assembled a situation, action, and result."); }
            else if (Act == Act.Team) { Act = Act.GapIntro; Record.Add("You asked for help and shared the task."); }
            else if (Act == Act.Gap) Act = Act.GapChoice;
        }
        public void ChooseMemory(bool keep)
        {
            if (Act != Act.GapChoice) return;
            KeptMemory = keep;
            Record.Add(keep ? "You kept the months that made you human." : "You let the company redact the missing months.");
            Act = Act.Return;
        }
        public void ReturnToInterview() { if (Act == Act.Return) Act = Act.Offer; }
        public void Finish(bool boundary)
        {
            if (Act != Act.Offer) return;
            SetBoundary = boundary; Completed = true; Act = Act.Ending;
        }
        public void Catch()
        {
            if (!IsChase) return;
            Catches++; Act = Act.Caught;
        }
        public void Retry() { if (Act == Act.Caught) { Act = checkpoint; ResetQuestion(); } }
        public string EndingTitle => SetBoundary ? "OUT OF OFFICE." : KeptMemory ? "YOU KEPT SOMETHING." : "WELCOME TO THE TEAM.";
        public string EndingText => SetBoundary
            ? "You give them your hours. They ask for the rest.\n\nFor the first time today, you leave a question unanswered. The exit opens. Behind you, a pen clicks. You keep walking."
            : KeptMemory
                ? "The contract prints with one blank line. You write a time to go home.\n\nIn your pocket is a page they could not file. In the hallway, Iris looks up. For a moment, she remembers her own name."
                : "Your calendar fills itself. Lunch disappears. Then Saturday. Then the months you crossed out.\n\nTomorrow, you will stand by the printer. A new candidate will arrive. You already know what to say: We are a family here.";
    }
}
