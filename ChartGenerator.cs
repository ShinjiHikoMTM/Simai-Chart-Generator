using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace SiMaiGenerator
{
    public class ChartGenerator
    {
        private Random rnd = new Random();

        // Patterns: Stream (circular), Trill (left-right), Resting
        private enum PatternType { Stream, Trill, Resting }
        private PatternType currentPattern = PatternType.Stream;

        private int patternCounter = 0;
        private int patternDirection = 1;

        private int slideCooldown = 0;
        private int silenceCounter = 0;

        // Break Cooldown
        private int breakCooldown = 0;

        private int[] keyBusyUntil = new int[9];
        private int[] lastActionTimeOnKey = new int[9];

        private int currentSlideStart = -1;
        private int currentSlideEnd = -1;

        private int lastPos = 1;

        public string Generate(AudioAnalyzer analyzer, int targetBpm, double totalSeconds, int levelIndex)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"({targetBpm})");

            // Parameters
            int division = 16;
            double volumeThreshold = 0.14;
            double spawnChance = 0.45;

            double pSlide = 0.15;
            double pHold = 0.08;
            double pDualRate = 0.0;
            double pTouch = 0.0;

            bool highBpmMode = targetBpm > 150;


            switch (levelIndex)
            {
                case 0: // EASY
                    division = 4; volumeThreshold = 0.20; spawnChance = 0.20; pHold = 0.40;
                    break;
                case 1: // BASIC
                    division = 8; volumeThreshold = 0.18; spawnChance = 0.25; pHold = 0.10; pSlide = 0.05;
                    break;
                case 2: // ADVANCED
                    division = 8; volumeThreshold = 0.16; spawnChance = 0.30; pSlide = 0.10; pHold = 0.08; pDualRate = 0.10; pTouch = 0.01;
                    break;
                case 3: // EXPERT 
                    division = 16; volumeThreshold = 0.14; spawnChance = 0.35; pSlide = 0.15; pHold = 0.08; pDualRate = 0.15; pTouch = 0.015;
                    break;
                case 4: // MASTER
                    division = 16; volumeThreshold = 0.10; spawnChance = 0.40; pSlide = 0.22; pHold = 0.08; pDualRate = 0.16; pTouch = 0.02;
                    break;
                case 5: // Re:MASTER
                    division = 16; volumeThreshold = 0.08; spawnChance = 0.45; pSlide = 0.25; pHold = 0.08; pDualRate = 0.17; pTouch = 0.03;
                    break;
            }

            // Dynamic density adjustment based on BPM
            if (targetBpm > 150)
            {
                // Calculate the portion exceeding 150
                double bpmOver = targetBpm - 150;

                // Add an extra 0.002 for every additional 1 BPM (2% increase for every 10 BPM)
                double multiplier = 1.02 + (bpmOver * 0.002);

                // Set an upper limit to prevent irrational behavior at BPM 300 (maximum 1.3x)
                if (multiplier > 1.3) multiplier = 1.3;

                spawnChance *= multiplier;
            }
            else if (targetBpm < 100)
            {
                spawnChance *= 1.1;
            }
            if (spawnChance > 0.95) spawnChance = 0.95;

            sb.Append($"{{{division}}}");

            double secondsPerBeat = 60.0 / targetBpm;
            double secondsPerSlot = secondsPerBeat * (4.0 / division);
            int totalSlots = (int)(totalSeconds / secondsPerSlot);

            ResetState();

            float localMaxVolume = 0f;

            for (int i = 0; i < totalSlots; i++)
            {
                UpdatePatternState(i, division);

                if (slideCooldown > 0) slideCooldown--;
                else { currentSlideStart = -1; currentSlideEnd = -1; }

                if (breakCooldown > 0) breakCooldown--;

                double currentTime = i * secondsPerSlot;
                float currentVolume = analyzer.GetVolumeAt(currentTime, 0.05);
                localMaxVolume = Math.Max(localMaxVolume * 0.98f, currentVolume);

                bool isMeasureStart = (i % 16 == 0);
                bool isQuarter = (i % 4 == 0);
                bool isEighth = (i % 2 == 0);

                // --- 1. Spawn Check ---
                double probMult = 1.0;
                if (slideCooldown > 0) probMult = 0.1;

                if (highBpmMode)
                {
                    if (isQuarter) probMult *= 1.2;
                    else if (isEighth) probMult *= 0.8;
                    else
                    {
                        if (currentPattern == PatternType.Trill) probMult *= 0.2;
                        else probMult = 0.0;
                    }
                }

                bool shouldSpawn = false;
                if (currentVolume > volumeThreshold * 0.9 && (rnd.NextDouble() < (spawnChance * probMult))) shouldSpawn = true;
                if (isQuarter && currentVolume > volumeThreshold * 1.5) shouldSpawn = true;

                if (!shouldSpawn)
                {
                    sb.Append(",");
                    FormatLine(sb, i, division);
                    continue;
                }

                // --- 2. Break Check ---
                string suffix = "";
                if (isQuarter)
                {
                    bool allowBreak = false;
                    if (isMeasureStart && currentVolume > volumeThreshold * 0.8) allowBreak = true;
                    else if (breakCooldown <= 0 && currentVolume >= localMaxVolume * 0.95 && rnd.NextDouble() < 0.4) allowBreak = true;

                    if (allowBreak)
                    {
                        suffix = "b";
                        breakCooldown = division * rnd.Next(2, 5);
                    }
                }

                // --- 3. Position Decision ---
                int mainPos = GetErgonomicNextPos(lastPos);
                mainPos = GetSmartFreeKey(mainPos, i);

                if (mainPos == -1)
                {
                    sb.Append(",");
                    FormatLine(sb, i, division);
                    continue;
                }

                string noteContent = "";

                // A. Touch (Fixed Syntax Error)
                if (pTouch > 0 && isQuarter && slideCooldown <= 0 && rnd.NextDouble() < pTouch)
                {
                    string region = rnd.NextDouble() > 0.6 ? "C" : $"B{mainPos}";
                    // Fix: NEVER add suffix ("b") to Touch notes to avoid "Cb", "B4b" errors
                    noteContent = $"{region}";
                }
                // B. Slide (Star)
                else if (pSlide > 0 && isQuarter && slideCooldown <= 0 && rnd.NextDouble() < pSlide && !IsJack(mainPos, i))
                {
                    if (suffix == "" && isMeasureStart && breakCooldown <= 0)
                    {
                        suffix = "b";
                        breakCooldown = division * 4;
                    }

                    // Call corrected GenerateSafeSlide
                    noteContent = GenerateSafeSlide(mainPos, suffix, highBpmMode, levelIndex, out int endPos, out int durationSlots);
                    lastPos = mainPos;

                    slideCooldown = durationSlots + 4;
                    currentSlideStart = mainPos;
                    currentSlideEnd = endPos;
                    LockKeyAndNeighbors(mainPos, i + durationSlots + 2);
                    LockKeyAndNeighbors(endPos, i + durationSlots + 2);
                }
                // C. Hold
                else if (pHold > 0 && slideCooldown <= 0 && rnd.NextDouble() < pHold && CheckVolumeSustain(analyzer, currentTime, secondsPerSlot, 4))
                {
                    int len = 4;
                    noteContent = $"{mainPos}{suffix}h[{division}:{len}]";
                    LockKeyAndNeighbors(mainPos, i + len + 2);
                    lastPos = mainPos;
                }
                // D. Tap
                else
                {
                    noteContent = $"{mainPos}{suffix}";
                    lastPos = mainPos;
                    lastActionTimeOnKey[mainPos] = i;
                    keyBusyUntil[mainPos] = i + 2;
                }

                // E. Dual
                if (!noteContent.Contains("C") && !noteContent.Contains("B") &&
                    pDualRate > 0 && slideCooldown <= 0 && !noteContent.Contains("Slide") && !noteContent.Contains("h") && rnd.NextDouble() < pDualRate)
                {
                    if (!highBpmMode || isEighth)
                    {
                        int dualPos = GetSafeDualPos(mainPos);
                        if (IsKeySafe(dualPos, i) && !IsBottomSpam(mainPos, dualPos) && !IsVerticalSpam(mainPos, dualPos) && !IsJack(dualPos, i))
                        {
                            string dualSuffix = (suffix == "b") ? "b" : "";

                            // Skip dual if adjacent break (hard to read)
                            if (suffix == "b" && IsAdjacent(mainPos, dualPos))
                            {
                                // Do nothing
                            }
                            else
                            {
                                noteContent = $"{noteContent}/{dualPos}{dualSuffix}";
                                lastActionTimeOnKey[dualPos] = i;
                                keyBusyUntil[dualPos] = i + 2;
                            }
                        }
                    }
                }

                sb.Append(noteContent);
                sb.Append(",");
                FormatLine(sb, i, division);
            }

            sb.Append("E");
            return sb.ToString();
        }

        // --- Core Methods ---

        // Fix: GenerateSafeSlide handles 7s7, 1V1 path errors
        private string GenerateSafeSlide(int start, string suffix, bool highBpm, int levelIndex, out int endPos, out int durationSlots)
        {
            // 1. Define Shapes (Removed unstable s, z, V)
            string[] simpleShapes = { "-", "^", "v" }; // Simple: Line, Arcs
            string[] complexShapes = { "<", ">", "p", "q" }; // Complex: Fans, Lightning

            // 2. Determine Complexity
            double simpleRate = 1.0;
            switch (levelIndex)
            {
                case 3: simpleRate = 0.70; break; // Expert
                case 4: simpleRate = 0.55; break; // Master
                case 5: simpleRate = 0.50; break; // Re:Master
            }

            string shape;
            if (rnd.NextDouble() < simpleRate) shape = simpleShapes[rnd.Next(simpleShapes.Length)];
            else shape = complexShapes[rnd.Next(complexShapes.Length)];

            // 3. Calculate Valid End Position
            int end = start;

            if (shape == "-")
            {
                end = (start + 4 - 1) % 8 + 1; // Line: Opposite side (1->5)
            }
            else if (shape == "^" || shape == "v")
            {
                end = (start + 2 - 1) % 8 + 1; // Arc: Skip 1 key (1->3)
            }
            else if (shape == "<" || shape == ">")
            {
                end = (start + 2 - 1) % 8 + 1; // Fan: Skip 1 key (1->3)
            }
            else if (shape == "p" || shape == "q")
            {
                end = (start + 4 - 1) % 8 + 1; // Lightning: Opposite side (1->5)
            }

            // 4. Safety Check: Never allow start == end
            if (end == start)
            {
                shape = "-";
                end = (start + 4 - 1) % 8 + 1;
            }

            endPos = end;

            // 5. Slide Speed ​​Logic (Global Smoothness Optimization)
            string durationStr;
            double speedRoll = rnd.NextDouble();


            if (speedRoll < 0.15)
            {
                durationStr = "[16:1]";
                durationSlots = 1; 
            }
            else if (speedRoll < 0.60) 
            {
                durationStr = "[8:1]";
                durationSlots = 2;
            }
            else // 40%
            {
                durationStr = "[4:1]";
                durationSlots = 4; 
            }

            return $"{start}{suffix}{shape}{end}{durationStr}";
        }
        private string GetProximityTouchPos(int pos)
        {
            if (rnd.NextDouble() < 0.5) return "C";
            int offset = rnd.Next(-1, 2);
            int targetB = (pos + offset - 1 + 8) % 8 + 1;
            return $"B{targetB}";
        }

        private bool IsJack(int pos, int currentIndex) => lastActionTimeOnKey[pos] >= currentIndex - 2;
        private bool IsAdjacent(int p1, int p2) { int diff = Math.Abs(p1 - p2); return diff == 1 || diff == 7; }
        private void ResetState() { lastPos = 1; patternCounter = 0; slideCooldown = 0; silenceCounter = 0; breakCooldown = 0; currentSlideStart = -1; currentSlideEnd = -1; for (int k = 0; k < 9; k++) { keyBusyUntil[k] = -1; lastActionTimeOnKey[k] = -999; } }
        private void LockKeyAndNeighbors(int pos, int untilIndex) { keyBusyUntil[pos] = untilIndex; lastActionTimeOnKey[pos] = untilIndex; int left = (pos - 2 + 8) % 8 + 1; int right = (pos % 8) + 1; if (keyBusyUntil[left] < untilIndex - 1) keyBusyUntil[left] = untilIndex - 1; if (keyBusyUntil[right] < untilIndex - 1) keyBusyUntil[right] = untilIndex - 1; }
        private bool IsBottomSpam(int p1, int p2) => (p1 == 4 && p2 == 5) || (p1 == 5 && p2 == 4);
        private bool IsVerticalSpam(int p1, int p2) { int diff = Math.Abs(p1 - p2); return diff == 5 || diff == 3; }
        private int GetErgonomicNextPos(int current) { int next = current; if (currentPattern == PatternType.Trill) { next = 9 - current; if (IsBottomSpam(current, next)) next = 3; } else { int move = 1; if (rnd.NextDouble() > 0.9) move = 2; next = current + (move * patternDirection); while (next > 8) next -= 8; while (next < 1) next += 8; } return next; }
        private int GetSafeDualPos(int pos) { int[] preferred = new int[] { }; switch (pos) { case 1: preferred = new int[] { 8, 6, 2 }; break; case 2: preferred = new int[] { 7, 3, 1 }; break; case 3: preferred = new int[] { 2, 6, 8 }; break; case 4: preferred = new int[] { 3, 6 }; break; case 5: preferred = new int[] { 6, 3 }; break; case 6: preferred = new int[] { 5, 3, 1 }; break; case 7: preferred = new int[] { 2, 6, 8 }; break; case 8: preferred = new int[] { 1, 7, 3 }; break; } foreach (int p in preferred) { if (IsKeySafe(p, -1)) return p; } return (pos % 8) + 1; }
        private bool IsKeySafe(int pos, int currentIndex) { if (keyBusyUntil[pos] > currentIndex) return false; if (currentSlideStart != -1) { if (pos == currentSlideStart || pos == currentSlideEnd) return false; int sL = (currentSlideStart - 2 + 8) % 8 + 1; int sR = (currentSlideStart % 8) + 1; int eL = (currentSlideEnd - 2 + 8) % 8 + 1; int eR = (currentSlideEnd % 8) + 1; if (pos == sL || pos == sR || pos == eL || pos == eR) return false; } return true; }
        private int GetSmartFreeKey(int startPos, int currentIndex) { int[] offsets = { 0, 1, -1, 2, -2 }; foreach (int off in offsets) { int check = startPos + off; while (check > 8) check -= 8; while (check < 1) check += 8; if (IsJack(check, currentIndex)) continue; if (IsKeySafe(check, currentIndex)) return check; } return -1; }
        private void FormatLine(StringBuilder sb, int index, int division) { if ((index + 1) % division == 0) sb.Append("\n"); }
        private void UpdatePatternState(int index, int division) { if (patternCounter > 0) { patternCounter--; return; } patternCounter = division * rnd.Next(2, 6); patternDirection = rnd.Next(0, 2) == 0 ? 1 : -1; double r = rnd.NextDouble(); if (r < 0.6) currentPattern = PatternType.Stream; else if (r < 0.9) currentPattern = PatternType.Trill; else currentPattern = PatternType.Resting; }
        private bool CheckVolumeSustain(AudioAnalyzer analyzer, double startTime, double step, int checkCount) { for (int k = 1; k <= checkCount; k++) if (analyzer.GetVolumeAt(startTime + k * step) < 0.1) return false; return true; }
    }
}