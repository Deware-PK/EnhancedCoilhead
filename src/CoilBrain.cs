using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GameNetcodeStuff;
using UnityEngine;

namespace EnhancedCoilhead
{
    // Per-Coil-Head state: patrol sweep, noise investigation, victim selection.
    internal sealed class CoilBrain
    {
        private static readonly ConditionalWeakTable<SpringManAI, CoilBrain> Table =
            new ConditionalWeakTable<SpringManAI, CoilBrain>();

        public static CoilBrain Get(SpringManAI c) => Table.GetValue(c, _ => new CoilBrain());

        private const float ArriveDistance = 3.5f;
        private const float WaypointTimeout = 25f;
        private const float VisitRadius = 8f;

        private enum Mode { Patrol, Investigate }

        private Mode _mode = Mode.Patrol;
        private bool _hasGoal;
        private Vector3 _goal;
        private float _goalDeadline;
        private float _goalPriority;
        private bool _hijacked;

        private int _prevState;
        private bool _hadTarget;
        private Vector3 _lastSeenPos;
        private float _nextSample;

        private readonly Dictionary<int, float> _visited = new Dictionary<int, float>();

        private PlayerControllerB _currentTarget;
        private float _lockUntil;

        // ---------- called every frame (Update postfix) ----------
        public void OnFrame(SpringManAI c)
        {
            ApplyAudio(c); // local on every machine

            if (!c.IsServer || c.isEnemyDead) return;
            int state = c.currentBehaviourStateIndex;

            if (state == 1 && Time.time >= _nextSample)
            {
                _nextSample = Time.time + 0.25f;
                var near = NearestTargetable(c, 80f);
                if (near != null)
                {
                    _lastSeenPos = near.transform.position;
                    _hadTarget = true;
                }
            }

            // Chase just ended: go check the last place a player was near us.
            if (_prevState == 1 && state == 0)
            {
                if (_hadTarget && Tuning.SmartPatrol.Value)
                    SetGoal(_lastSeenPos, Mode.Investigate, 0.5f);
                _hadTarget = false;
            }
            _prevState = state;
        }

        // ---------- audio ----------
        private readonly List<AudioSource> _srcs = new List<AudioSource>();
        private readonly List<float> _baseVol = new List<float>();
        private readonly List<float> _baseMin = new List<float>();
        private readonly List<float> _baseMax = new List<float>();
        private readonly HashSet<int> _seenSrc = new HashSet<int>();
        private float _nextAudioScan;

        // Captures every AudioSource on the enemy (and its children) and scales them live.
        private void ApplyAudio(SpringManAI c)
        {
            if (Time.time >= _nextAudioScan)
            {
                _nextAudioScan = Time.time + 3f;
                foreach (var a in c.GetComponentsInChildren<AudioSource>(true))
                {
                    if (a == null || !_seenSrc.Add(a.GetInstanceID())) continue;
                    _srcs.Add(a);
                    _baseVol.Add(a.volume);
                    _baseMin.Add(a.minDistance);
                    _baseMax.Add(a.maxDistance);
                    Plugin.Log.LogInfo($"[audio] captured '{a.gameObject.name}' vol={a.volume:F2} dist={a.minDistance:F1}-{a.maxDistance:F1} loop={a.loop} playing={a.isPlaying}");
                }
            }

            float v = Tuning.VolumeMultiplier.Value;
            float d = Tuning.AudibleDistanceMultiplier.Value;
            for (int i = 0; i < _srcs.Count; i++)
            {
                var a = _srcs[i];
                if (a == null) continue;
                a.volume = Mathf.Clamp01(_baseVol[i] * v);
                a.minDistance = _baseMin[i] * d;
                a.maxDistance = _baseMax[i] * d;
            }
        }

        // ---------- called after every DoAIInterval ----------
        public void OnInterval(SpringManAI c)
        {
            if (!c.IsServer) return;
            if (c.isEnemyDead || c.currentBehaviourStateIndex != 0) { Release(c); return; }
            if (c.onCooldownPhase > 0f) return; // vanilla holds position while resting

            bool wantControl = Tuning.SmartPatrol.Value || (_mode == Mode.Investigate && _hasGoal);
            if (!wantControl) { Release(c); return; }

            if (_hasGoal)
            {
                bool arrived = Flat(c.transform.position, _goal) < ArriveDistance;
                if (arrived || Time.time > _goalDeadline)
                {
                    if (Tuning.LogNoises.Value && _mode == Mode.Investigate)
                        Plugin.Log.LogInfo(arrived ? "[noise] arrived at noise" : "[noise] gave up (timeout)");
                    MarkVisitedNear(c, _goal);
                    _hasGoal = false;
                    _mode = Mode.Patrol;
                }
            }

            if (!_hasGoal)
            {
                if (!Tuning.SmartPatrol.Value || !PickPatrolGoal(c)) { Release(c); return; }
            }

            Hijack(c);
            if (!c.SetDestinationToPosition(_goal, true))
            {
                MarkVisitedNear(c, _goal); // unreachable, skip it
                _hasGoal = false;
                _mode = Mode.Patrol;
            }
        }

        // ---------- noise ----------
        // viaClient: true when a client relayed this noise (see NetSync), false when the host heard it itself.
        public void Hear(SpringManAI c, Vector3 pos, float loudness, int noiseID, bool viaClient)
        {
            string via = viaClient ? "client" : "local";
            if (Tuning.LogNoises.Value)
                Plugin.Log.LogInfo($"[noise] loudness={loudness:F2} id={noiseID} dist={Vector3.Distance(c.transform.position, pos):F1} state={c.currentBehaviourStateIndex} via={via}");

            if (!Tuning.NoiseHearing.Value) return;
            if (!c.IsServer || c.isEnemyDead) return;
            if (c.currentBehaviourStateIndex != 0 || c.onCooldownPhase > 0f) return;
            float min = noiseID == NetSync.VoiceNoiseId ? Tuning.MinVoiceLoudness.Value : Tuning.MinNoiseLoudness.Value;
            if (loudness < min) return;

            // Don't let a quiet noise override a louder one we're still heading to.
            if (_mode == Mode.Investigate && _hasGoal && Time.time < _goalDeadline && loudness < _goalPriority * 0.6f)
                return;

            SetGoal(pos, Mode.Investigate, loudness);
            if (Tuning.LogNoises.Value)
                Plugin.Log.LogInfo($"[noise] -> investigating {pos} (loudness {loudness:F2}, id {noiseID}, via={via})");
        }

        // ---------- victim selection ----------
        public void ReTarget(SpringManAI c)
        {
            if (!Tuning.SmartTargeting.Value) return;
            var players = StartOfRound.Instance.allPlayerScripts;
            if (players == null) return;

            if (_currentTarget != null && Time.time < _lockUntil && c.PlayerIsTargetable(_currentTarget))
            {
                c.targetPlayer = _currentTarget;
                return;
            }

            Vector3 eye = c.transform.position + Vector3.up * 0.6f;
            PlayerControllerB best = null;
            float bestScore = float.MaxValue, curScore = float.MaxValue;

            foreach (var p in players)
            {
                if (p == null || !c.PlayerIsTargetable(p)) continue;
                float s = Score(c, p, players, eye);
                if (p == _currentTarget) curScore = s;
                if (s < bestScore) { bestScore = s; best = p; }
            }
            if (best == null) return;

            // Hysteresis: only switch if the new victim is clearly better.
            if (_currentTarget != null && curScore < float.MaxValue && bestScore > curScore * 0.75f)
                best = _currentTarget;

            _currentTarget = best;
            _lockUntil = Time.time + Tuning.TargetHoldTime.Value;
            c.targetPlayer = best;
        }

        private static float Score(SpringManAI c, PlayerControllerB p, PlayerControllerB[] players, Vector3 eye)
        {
            float s = Vector3.Distance(c.transform.position, p.transform.position);

            if (p.HasLineOfSightToPosition(eye, 68f))
                s *= Tuning.WatchedPenalty.Value;

            float nearestMate = float.MaxValue;
            foreach (var q in players)
            {
                if (q == null || q == p || q.isPlayerDead || !q.isPlayerControlled) continue;
                float d = Vector3.Distance(p.transform.position, q.transform.position);
                if (d < nearestMate) nearestMate = d;
            }
            if (nearestMate > Tuning.IsolationRange.Value)
                s *= Tuning.IsolationFactor.Value;

            return s;
        }

        // ---------- helpers ----------
        private void SetGoal(Vector3 pos, Mode mode, float priority)
        {
            _mode = mode;
            _goal = pos;
            _hasGoal = true;
            _goalPriority = priority;
            _goalDeadline = Time.time + (mode == Mode.Investigate ? Tuning.InvestigateTimeout.Value : WaypointTimeout);
        }

        private bool PickPatrolGoal(SpringManAI c)
        {
            var nodes = c.allAINodes;
            if (nodes == null || nodes.Length == 0) return false;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                float now = Time.time;
                Vector3 pos = c.transform.position;
                GameObject best = null;
                float bestD = float.MaxValue;

                foreach (var n in nodes)
                {
                    if (n == null) continue;
                    int id = n.GetInstanceID();
                    if (_visited.TryGetValue(id, out float t) && now - t < Tuning.PatrolMemory.Value) continue;

                    float d = (n.transform.position - pos).sqrMagnitude;
                    if (d < 36f) { _visited[id] = now; continue; } // already standing here

                    d *= Random.Range(0.85f, 1.15f); // a little variety, still a sweep
                    if (d < bestD) { bestD = d; best = n; }
                }

                if (best != null)
                {
                    SetGoal(best.transform.position, Mode.Patrol, 0f);
                    return true;
                }
                _visited.Clear(); // whole map swept: start a new round
            }
            return false;
        }

        private void MarkVisitedNear(SpringManAI c, Vector3 p)
        {
            var nodes = c.allAINodes;
            if (nodes == null) return;
            float now = Time.time;
            foreach (var n in nodes)
                if (n != null && Flat(n.transform.position, p) < VisitRadius)
                    _visited[n.GetInstanceID()] = now;
        }

        private void Hijack(SpringManAI c)
        {
            var s = c.searchForPlayers;
            if (!_hijacked && s.inProgress) c.StopSearch(s);
            s.inProgress = true; // makes vanilla skip its own random search restart
            _hijacked = true;
            c.movingTowardsTargetPlayer = false;
        }

        private void Release(SpringManAI c)
        {
            if (!_hijacked) return;
            _hijacked = false;
            c.searchForPlayers.inProgress = false; // vanilla restarts its search by itself
        }

        private static PlayerControllerB NearestTargetable(SpringManAI c, float maxDist)
        {
            PlayerControllerB best = null;
            float bestD = maxDist;
            var players = StartOfRound.Instance.allPlayerScripts;
            if (players == null) return null;
            foreach (var p in players)
            {
                if (p == null || !c.PlayerIsTargetable(p)) continue;
                float d = Vector3.Distance(c.transform.position, p.transform.position);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
