using System;
using System.Text;
using System.Collections.Generic;

namespace SiMaiGenerator
{
    public class ChartGenerator
    {
        private Random rnd = new Random();

        private enum PatternType { Random, Stream, Trill, Zigzag, Resting }
        private PatternType currentPattern = PatternType.Random;
        private int patternCounter = 0;
        private int patternDirection = 1;

        private int centerHoldLockTimer = 0;
        private int touchHoldCooldown = 0;
        private int slideCooldown = 0;
        private int touchCooldown = 0; 
        private int lastSlideEndPos = -1;
        private int[] keyBusyUntil = new int[9];
        private int lastTouchPos = -1;
        private double lastNoteTime = 0.0;

        public string Generate(AudioAnalyzer analyzer, int targetBpm, double totalSeconds, int levelIndex)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append($"({targetBpm})");

            int division = 4;
            double volumeThreshold = 0.2;
            double spawnChance = 0.5;
            double pSlide = 0.0, pTouch = 0.0, pHold = 0.0, pTouchHold = 0.0;
            double pDualRate = 0.0;

            bool allowComplexSlide = false;
            bool allowOuterTouch = false;
            bool forceAdjacent = false;
            bool slowSlide = false;
            bool allowEx = false;
            bool allowDual = false;

            int chartStyle = rnd.Next(0, 3);
            double styleMultiplier = 1.0;
            if (chartStyle == 0) styleMultiplier = 0.85;
            else if (chartStyle == 2) styleMultiplier = 1.15;

            double randomFlux = (rnd.NextDouble() * 0.1) - 0.05;

            switch (levelIndex)
            {
                case 0: // EASY
                    division = 8;
                    volumeThreshold = 0.18;
                    spawnChance = 0.20;
                    pHold = 0.50;
                    pSlide = 0.01;
                    pDualRate = 0.0;
                    forceAdjacent = true; slowSlide = true; allowEx = false;
                    break;

                case 1: // BASIC
                    division = 8;
                    volumeThreshold = 0.18;
                    spawnChance = 0.35;
                    pHold = 0.30;
                    pSlide = 0.05;
                    pDualRate = 0.08;
                    forceAdjacent = true; slowSlide = true; allowComplexSlide = false;
                    allowDual = true; allowEx = true;
                    break;

                case 2: // ADVANCED
                    division = 8;
                    volumeThreshold = 0.16;
                    spawnChance = 0.50;
                    pSlide = 0.12;
                    pHold = 0.25;
                    pTouch = 0.05;
                    pTouchHold = 0.02;
                    pDualRate = 0.15;
                    forceAdjacent = true; allowDual = true; allowEx = true;
                    break;

                case 3: // EXPERT
                    division = 16;
                    volumeThreshold = 0.14;
                    spawnChance = 0.38;

                    pSlide = 0.15;
                    pHold = 0.18;
                    pTouch = 0.01;
                    pTouchHold = 0.05;
                    pDualRate = 0.15;

                    forceAdjacent = true;
                    slowSlide = false;
                    allowComplexSlide = false;
                    allowDual = true; allowEx = true; allowOuterTouch = false;
                    break;

                case 4: // MASTER
                    division = 16;
                    volumeThreshold = 0.12;
                    spawnChance = 0.50;

                    pSlide = 0.20;
                    pHold = 0.12;
                    pTouch = 0.025;
                    pTouchHold = 0.05;
                    pDualRate = 0.30;

                    forceAdjacent = true;
                    slowSlide = false;
                    allowComplexSlide = true;
                    allowDual = true; allowEx = true; allowOuterTouch = true;
                    break;

                case 5: // Re:MASTER
                    division = 16;
                    volumeThreshold = 0.08;
                    spawnChance = 0.60;

                    pSlide = 0.25;
                    pHold = 0.05;
                    pTouch = 0.05;
                    pTouchHold = 0.05;
                    pDualRate = 0.40;

                    forceAdjacent = false;
                    slowSlide = false;
                    allowComplexSlide = true;
                    allowDual = true; allowEx = true; allowOuterTouch = true;
                    break;
            }

            spawnChance = (spawnChance * styleMultiplier) + randomFlux;
            spawnChance = Math.Max(0.1, Math.Min(0.90, spawnChance));

            sb.Append($"{{{division}}}");

            double secondsPerBeat = 60.0 / targetBpm;
            double secondsPerSlot = secondsPerBeat * (4.0 / division);
            int totalSlots = (int)(totalSeconds / secondsPerSlot);

            int lastPos = 1;
            int skipSlots = 0;

            // Status Reset
            centerHoldLockTimer = 0;
            touchHoldCooldown = 0;
            slideCooldown = 0;
            touchCooldown = 0;
            lastSlideEndPos = -1;
            lastTouchPos = -1;
            lastNoteTime = 0.0;
            for (int k = 0; k < 9; k++) keyBusyUntil[k] = -1;

            for (int i = 0; i < totalSlots; i++)
            {
                UpdatePatternState(i, division, levelIndex);
                if (centerHoldLockTimer > 0) centerHoldLockTimer--;
                if (touchHoldCooldown > 0) touchHoldCooldown--;
                if (slideCooldown > 0) slideCooldown--;
                if (touchCooldown > 0) touchCooldown--;

                if (skipSlots > 0)
                {
                    sb.Append(",");
                    FormatLine(sb, i, division);
                    skipSlots--;
                    continue;
                }

                bool isResting = (currentPattern == PatternType.Resting && rnd.NextDouble() < 0.6);

                double currentTime = i * secondsPerSlot;
                float currentVolume = analyzer.GetVolumeAt(currentTime, 0.05);

                bool forceSpawn = false;
                double timeSinceLast = currentTime - lastNoteTime;

                if (timeSinceLast > 1.5 && currentVolume > 0.02)
                {
                    forceSpawn = true;
                    isResting = false;
                }

                if (isResting && !forceSpawn)
                {
                    sb.Append(",");
                    FormatLine(sb, i, division);
                    continue;
                }

                double dynamicChance = spawnChance;
                if (chartStyle == 0 && currentVolume < volumeThreshold * 1.5) dynamicChance *= 0.6;
                else if (currentVolume < volumeThreshold * 1.5) dynamicChance *= 0.8;

                bool volumeCheck = (currentVolume > volumeThreshold) && (rnd.NextDouble() < (currentVolume * dynamicChance + 0.1));
                bool shouldSpawn = forceSpawn || volumeCheck;

                if (shouldSpawn)
                {
                    lastNoteTime = currentTime;
                    bool isBreak = (currentVolume > 0.90);
                    if (levelIndex == 0 && rnd.NextDouble() > 0.05) isBreak = false;
                    bool isEx = !isBreak && allowEx && (rnd.NextDouble() < 0.2);

                    string suffix = "";
                    if (isBreak) suffix = "b";
                    else if (isEx) suffix = "x";

                    string mainNote = "";
                    int mainPos = GetNextPos(lastPos, forceAdjacent);
                    mainPos = GetFreeKey(mainPos, i);

                    double typeRoll = rnd.NextDouble();
                    bool isMainNoteTouchHold = false;

                    // === Touch Hold Limit Logic ===
                    bool isOutro = (i > totalSlots * 0.90);
                    bool isQuietSection = (currentVolume < volumeThreshold * 0.6);
                    bool allowTouchHoldHere = isOutro || isQuietSection;

                    // 1. Touch Hold
                    if (allowTouchHoldHere && pTouchHold > 0 && rnd.NextDouble() < 0.2 && centerHoldLockTimer == 0 && touchHoldCooldown == 0)
                    {
                        int len = (division / 4) * rnd.Next(4, 9);
                        mainNote = $"Ch[{division}:{len}]";
                        skipSlots = len - 1;
                        centerHoldLockTimer = len;
                        touchHoldCooldown = len + (division * 8);
                        isMainNoteTouchHold = true;
                    }
                    // 2. Slide
                    else if (pSlide > 0 && typeRoll < pSlide && slideCooldown == 0)
                    {
                        if (keyBusyUntil[mainPos] >= i) mainPos = GetFreeKey(mainPos, i);
                        if (mainPos == lastSlideEndPos) mainPos = (mainPos % 8) + 1;
                        mainPos = GetFreeKey(mainPos, i);

                        string slideSuffix = isBreak ? "b" : "";
                        mainNote = GenerateValidSlide(mainPos, allowComplexSlide, slideSuffix, slowSlide, out int slideEnd);
                        lastPos = mainPos;
                        lastSlideEndPos = slideEnd;

                        if (levelIndex <= 3) slideCooldown = division * 2;
                        else slideCooldown = division;
                    }
                    // 3. Touch 
                    else if (pTouch > 0 && typeRoll < (pSlide + pTouch) && touchCooldown == 0)
                    {
                        mainNote = GenerateImprovedTouch("", lastPos, allowOuterTouch);

                        // Set Touch Cooldowntime
                        touchCooldown = division * 2;
                    }
                    // 4. Hold
                    else if (pHold > 0 && typeRoll < (pSlide + pTouch + pHold))
                    {
                        if (keyBusyUntil[mainPos] >= i) mainPos = GetFreeKey(mainPos, i);

                        int holdLen = 0;
                        int maxCheck = (division == 16) ? 8 : 4;
                        for (int k = 1; k <= maxCheck; k++)
                        {
                            if (i + k >= totalSlots) break;
                            if (analyzer.GetVolumeAt(currentTime + k * secondsPerSlot) > volumeThreshold * 0.8) holdLen++;
                            else break;
                        }

                        if (holdLen > 0)
                        {
                            mainNote = $"{mainPos}h[{division}:{holdLen}]";
                            keyBusyUntil[mainPos] = i + holdLen;
                            lastPos = mainPos;
                            skipSlots = holdLen - 1;
                        }
                        else
                        {
                            mainNote = $"{mainPos}{suffix}";
                            lastPos = mainPos;
                        }
                    }
                    // 5. Tap
                    else
                    {
                        if (forceSpawn) suffix = "";
                        if (keyBusyUntil[mainPos] >= i) mainPos = GetFreeKey(mainPos, i);
                        mainNote = $"{mainPos}{suffix}";
                        lastPos = mainPos;
                    }

                    // 6. Dual Note
                    if (!forceSpawn && allowDual && !isMainNoteTouchHold && rnd.NextDouble() < pDualRate)
                    {
                        int dualPos;
                        if (mainNote.Contains("C") || mainNote.Contains("B") || mainNote.Contains("E"))
                        {
                            dualPos = (lastPos + 3) % 8 + 1;
                            dualPos = GetFreeKey(dualPos, i);
                            string dualSuffix = isBreak ? "b" : "";
                            sb.Append($"{mainNote}/{dualPos}{dualSuffix}");
                        }
                        else
                        {
                            int offset = rnd.Next(0, 2) == 0 ? 4 : 1;
                            dualPos = (lastPos + offset - 1) % 8 + 1;
                            if (dualPos == lastPos) dualPos = (dualPos % 8) + 1;
                            dualPos = GetFreeKey(dualPos, i);

                            if (dualPos == mainPos) sb.Append(mainNote);
                            else sb.Append($"{mainNote}/{dualPos}{suffix}");
                        }
                    }
                    else
                    {
                        sb.Append(mainNote);
                    }
                }

                sb.Append(",");
                FormatLine(sb, i, division);
            }

            sb.Append("E");
            return sb.ToString();
        }

        // Helper Methods
        private int GetFreeKey(int startPos, int currentIndex) { if (keyBusyUntil[startPos] < currentIndex) return startPos; for (int offset = 1; offset < 8; offset++) { int checkPos = (startPos + offset - 1) % 8 + 1; if (keyBusyUntil[checkPos] < currentIndex) return checkPos; } return startPos; }
        private void FormatLine(StringBuilder sb, int index, int division) { if ((index + 1) % division == 0) sb.Append("\n"); }
        private void UpdatePatternState(int currentIndex, int division, int levelIndex) { int switchInterval = division * 4; if (patternCounter <= 0) { patternCounter = switchInterval; double r = rnd.NextDouble(); if ((levelIndex == 2 || levelIndex == 3) && r < 0.1) { currentPattern = PatternType.Resting; return; } if (r < 0.4) currentPattern = PatternType.Stream; else if (r < 0.7) currentPattern = PatternType.Trill; else if (r < 0.85) currentPattern = PatternType.Zigzag; else currentPattern = PatternType.Random; patternDirection = rnd.Next(0, 2) == 0 ? 1 : -1; } patternCounter--; }
        private int GetNextPos(int current, bool forceAdjacent) { if (forceAdjacent) { int move = rnd.Next(0, 2) == 0 ? 1 : -1; if (rnd.NextDouble() < 0.3) move = 0; int next = current + move; while (next > 8) next -= 8; while (next < 1) next += 8; return next; } int pNext = current; switch (currentPattern) { case PatternType.Stream: pNext = current + patternDirection; break; case PatternType.Trill: pNext = current + 4; break; case PatternType.Zigzag: pNext = current + (2 * patternDirection); break; default: int rMove = rnd.Next(1, 4); pNext = rnd.Next(0, 2) == 0 ? current + rMove : current - rMove; break; } while (pNext > 8) pNext -= 8; while (pNext < 1) pNext += 8; return pNext; }

        private string GenerateValidSlide(int start, bool complex, string suffix, bool slow, out int slideEnd)
        {
            string duration = slow ? "[4:2]" : "[4:1]";
            bool forceSimple = !complex || (rnd.NextDouble() < 0.7);

            if (forceSimple)
            {
                int offset = rnd.Next(3, 6);
                int end = (start + offset - 1) % 8 + 1;
                slideEnd = end;
                return $"{start}{suffix}-{end}{duration}";
            }

            int type = rnd.Next(0, 5);
            string slideStr = "";
            int endPos = start;

            switch (type)
            {
                case 0: // ^ Arc
                    int offsetArc = rnd.Next(2, 4);
                    endPos = (start + offsetArc - 1) % 8 + 1;
                    slideStr = $"{start}{suffix}^{endPos}{duration}";
                    break;
                case 1: // > V
                    endPos = (start + 2) % 8 + 1;
                    slideStr = $"{start}{suffix}>{endPos}{duration}";
                    break;
                case 2: // v V-Inv
                    endPos = (start + 2) % 8 + 1;
                    slideStr = $"{start}{suffix}v{endPos}{duration}";
                    break;
                case 3: // p Loop
                    endPos = (start + 4) % 8 + 1;
                    slideStr = $"{start}{suffix}p{endPos}{duration}";
                    break;
                case 4: // q Loop
                    endPos = (start + 4) % 8 + 1;
                    slideStr = $"{start}{suffix}q{endPos}{duration}";
                    break;
            }

            slideEnd = endPos;
            return slideStr;
        }

        private string GenerateImprovedTouch(string suffix, int lastTapPos, bool allowOuter)
        {
            string finalSuffix = "";
            if (centerHoldLockTimer > 0) allowOuter = true;
            double roll = rnd.NextDouble();
            string selectedTouch = "";

            for (int retry = 0; retry < 3; retry++)
            {
                if (centerHoldLockTimer == 0 && roll < 0.3)
                {
                    selectedTouch = $"C{finalSuffix}";
                    if (lastTouchPos == 9) { roll = 1.0; continue; }
                    lastTouchPos = 9;
                    return selectedTouch;
                }

                int touchPos = rnd.Next(1, 9);
                if (touchPos == lastTapPos) touchPos = (touchPos % 8) + 1;

                string region = rnd.NextDouble() < 0.7 ? "B" : "E";

                int combinedPos = (region == "B" ? 10 : 20) + touchPos;
                if (combinedPos == lastTouchPos) continue;
                lastTouchPos = combinedPos;
                return $"{region}{touchPos}{finalSuffix}";
            }
            return $"B{(lastTapPos % 8) + 1}{finalSuffix}";
        }
    }
}