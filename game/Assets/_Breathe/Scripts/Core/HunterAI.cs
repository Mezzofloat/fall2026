using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breathe
{
    /// <summary>
    /// Mr. Vale's behaviour: patrols the office, investigates noise, chases on sight, searches after losing you.
    /// A closed door costs him <see cref="DoorDelay"/> seconds to open, unless the player is stressed: then he bursts through instantly.
    /// </summary>
    public sealed class HunterAI
    {
        public enum State { Patrol, Investigate, Chase, Search }
        public const float CatchDistance = .55f, SightRange = 9f, DoorDelay = 1f;
        public Vector2 Position { get; private set; }
        public State Mode { get; private set; } = State.Patrol;
        public Vector2 FocusPoint { get; private set; }
        public float PatrolSpeed = 1.7f, InvestigateSpeed = 2.3f, ChaseSpeed = 2.75f;
        readonly OfficeMap map;
        readonly System.Random rng;
        List<Vector2> path = new List<Vector2>();
        float repath, timer, sightLost, grace;
        bool stressed;
        Vector2 lastSeen;
        bool hasLastSeen;
        Door doorTarget; float doorWait;
        public bool WaitingAtDoor => doorTarget != null;
        public float DoorWaitLeft => doorTarget != null ? Mathf.Max(0f, doorWait) : 0f;
        public int DoorsOpened { get; private set; }
        public int DoorsBurst { get; private set; }

        public HunterAI(OfficeMap map, Vector2 start, int seed = 17, float headStart = 5f)
        {
            this.map = map; Position = start; rng = new System.Random(seed); grace = headStart; PickPatrol();
        }

        public bool Sees(Vector2 player, bool hidden) =>
            !hidden && Vector2.Distance(Position, player) <= SightRange && map.HasLineOfSight(Position, player);

        /// <summary>Returns true when the player is caught.</summary>
        public bool Tick(float dt, Vector2 player, bool hidden, float noiseRadius, float speedScale = 1f, bool stressed = false)
        {
            this.stressed = stressed;
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return false;
            dt = Mathf.Min(dt, .1f);
            if (grace > 0) { grace -= dt; return false; }
            float distance = Vector2.Distance(Position, player);
            bool sees = Sees(player, hidden);
            bool heard = distance <= noiseRadius && PathLength(player) <= noiseRadius * 1.8f;

            if (sees) { Mode = State.Chase; lastSeen = player; hasLastSeen = true; sightLost = 0; }
            else if (Mode == State.Chase)
            {
                sightLost += dt;
                if (sightLost > 1.2f) { Mode = State.Investigate; FocusPoint = hasLastSeen ? lastSeen : player; Repath(FocusPoint); }
            }
            if (!sees && heard && Mode != State.Chase) { Mode = State.Investigate; FocusPoint = player; Repath(FocusPoint); }

            switch (Mode)
            {
                case State.Chase: Follow(dt, player, ChaseSpeed * speedScale, .35f); break;
                case State.Investigate:
                    Follow(dt, FocusPoint, InvestigateSpeed * speedScale, .5f);
                    if (Vector2.Distance(Position, FocusPoint) < .8f || path.Count == 0 && timer <= 0) { Mode = State.Search; timer = 4.5f; }
                    break;
                case State.Search:
                    timer -= dt;
                    // Standing next to a hidden player for too long finds them.
                    if (hidden && distance < 1.6f && timer < 2.5f) return true;
                    if (timer <= 0) { Mode = State.Patrol; PickPatrol(); }
                    break;
                default:
                    Follow(dt, FocusPoint, PatrolSpeed * speedScale, 1f);
                    if (Vector2.Distance(Position, FocusPoint) < .8f || path.Count == 0 && timer <= 0) PickPatrol();
                    break;
            }
            timer -= Mode == State.Search ? 0 : dt;
            return distance < CatchDistance && (!hidden || Mode == State.Chase);
        }

        float PathLength(Vector2 to)
        {
            var p = map.FindPath(Position, to); if (p.Count == 0) return float.MaxValue;
            float length = 0; Vector2 previous = Position;
            foreach (var point in p) { length += Vector2.Distance(previous, point); previous = point; }
            return length;
        }

        void PickPatrol()
        {
            var points = map.Patrol; FocusPoint = points[rng.Next(points.Length)]; timer = 8f; Repath(FocusPoint);
        }

        void Repath(Vector2 to) { path = map.FindPath(Position, to); repath = .4f; }

        void Follow(float dt, Vector2 goal, float speed, float repathEvery)
        {
            repath -= dt;
            if (repath <= 0) { path = map.FindPath(Position, goal); repath = repathEvery; }
            // Aim at the centre of the current cell at corners to avoid clipping walls.
            Vector2 center = new Vector2(Mathf.Floor(Position.x) + .5f, Mathf.Floor(Position.y) + .5f);
            Vector2 target = path.Count > 0 ? path[0] : goal;
            if (Mathf.Abs(target.x - Position.x) > .05f && Mathf.Abs(target.y - Position.y) > .05f && Vector2.Distance(Position, center) > .03f) target = center;
            Vector2 toward = target - Position;
            if (toward.sqrMagnitude > 1e-6f && !HandleDoor(dt, toward.normalized)) return;
            Position = map.Move(Position, Vector2.ClampMagnitude(toward, speed * dt));
            if (path.Count > 0 && Vector2.Distance(Position, path[0]) < .08f) path.RemoveAt(0);
        }

        /// <summary>Returns false while he is standing at a closed door waiting to open it.</summary>
        bool HandleDoor(float dt, Vector2 direction)
        {
            Vector2 probe = Position + direction * .5f;
            var door = map.DoorAt(Mathf.FloorToInt(probe.x), Mathf.FloorToInt(probe.y));
            if (door == null || !door.Blocks) { doorTarget = null; return true; }
            if (doorTarget != door) { doorTarget = door; doorWait = stressed ? 0f : DoorDelay; }
            doorWait -= dt;
            if (doorWait > 0f) return false;
            map.HunterOpen(door, stressed); if (stressed) DoorsBurst++; else DoorsOpened++;
            doorTarget = null; return true;
        }

        public void Teleport(Vector2 position, float headStart) { Position = position; grace = headStart; Mode = State.Patrol; path.Clear(); doorTarget = null; PickPatrol(); }
    }
}
