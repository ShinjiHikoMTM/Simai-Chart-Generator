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
        private int silenceCounter = 0;

        private int touchSessionTimer = 0;
        private int touchPatternMode = 0;
        private int touchPatternStep = 1;
        private int lastTouchNum = 1;
        private int touchSessionIntervalCounter = 0;

        private int currentSessionCount = 0;
        private int maxSessionQuota = 0;
        private int minSessionQuota = 0;
        private int sessionGlobalCooldown = 0;

        private bool dualToggleState = false;
        private int dualToggleType = 0;

        private int lastSlideEndPos = -1;
        private int[] keyBusyUntil = new int[9];
        private int[] lastActionTimeOnKey = new int[9]; 

        private double lastNoteTime = 0.0;
        private int slideBusyUntil = -1;

        private int lastTapPos = -1;
        private int lastTapTimeIndex = -1;

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

            int sameKeyMinGap = 2;
            int complexSwitchGap = 4;

            int chartStyle = rnd.Next(0, 3);
            double styleMultiplier = 1.0;
            if (chartStyle == 0) styleMultiplier = 0.85;
            else if (chartStyle == 2) styleMultiplier = 1.15;

            double randomFlux = (rnd.NextDouble() * 0.1) - 0.05;

            maxSessionQuota = (int)(totalSeconds / 30.0);
            minSessionQuota = (int)(totalSeconds / 45.0);
            if (maxSessionQuota < 1) maxSessionQuota = 1;
            if (minSessionQuota < 1) minSessionQuota = 1;
            if (minSessionQuota > maxSessionQuota) minSessionQuota = maxSessionQuota;

            currentSessionCount = 0;
            sessionGlobalCooldown = 0;

            switch (levelIndex)
            {
                case 0: // EASY
                    division = 8; volumeThreshold = 0.18; spawnChance = 0.20;
                    pHold = 0.50; pSlide = 0.01; pDualRate = 0.0;
                    forceAdjacent = true; slowSlide = true; allowEx = false;
                    maxSessionQuota = 0;
                    break;

                case 1: // BASIC
                    division = 8; volumeThreshold = 0.18; spawnChance = 0.35;
                    pHold = 0.30; pSlide = 0.05; pDualRate = 0.08;
                    forceAdjacent = true; slowSlide = true; allowComplexSlide = false;
                    allowDual = true; allowEx = true;
                    maxSessionQuota = Math.Max(1, maxSessionQuota / 2);
                    break;

                case 2: // ADVANCED
                    division = 8; volumeThreshold = 0.16; spawnChance = 0.50;
                    pSlide = 0.12; pHold = 0.25;
                    pTouch = 0.05; pTouchHold = 0.02; pDualRate = 0.15;
                    forceAdjacent = true; allowDual = true; allowEx = true;
                    break;

                case 3: // EXPERT
                    division = 16; volumeThreshold = 0.14; spawnChance = 0.38;
                    pSlide = 0.15; pHold = 0.18;
                    pTouch = 0.02; pTouchHold = 0.05; pDualRate = 0.15;
                    forceAdjacent = true; slowSlide = false; allowComplexSlide = false;
                    allowDual = true; allowEx = true; allowOuterTouch = false;
                    break;

                case 4: // MASTER
                    division = 16; volumeThreshold = 0.12; spawnChance = 0.50;
                    pSlide = 0.20; pHold = 0.12;
                    pTouch = 0.05; pTouchHold = 0.05; pDualRate = 0.30;
                    forceAdjacent = true; slowSlide = false; allowComplexSlide = true;
                    allowDual = true; allowEx = true; allowOuterTouch = true;
                    break;

                case 5: // Re:MASTER
                    division = 16; volumeThreshold = 0.08; spawnChance = 0.60;
                    pSlide = 0.25; pHold = 0.05;
                    pTouch = 0.08; pTouchHold = 0.05; pDualRate = 0.40;
                    forceAdjacent = false; slowSlide = false; allowComplexSlide = true;
                    allowDual = true; allowEx = true; allowOuterTouch = true;
                    sameKeyMinGap = 1;
                    complexSwitchGap = 2;
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

            centerHoldLockTimer = 0;
            touchHoldCooldown = 0;
            slideCooldown = 0;
            touchSessionTimer = 0;
            touchSessionIntervalCounter = 0;
            silenceCounter = 0;
            lastSlideEndPos = -1;
            lastTouchNum = 1;
            lastNoteTime = 0.0;
            slideBusyUntil = -1;
            lastTapPos = -1;
            lastTapTimeIndex = -1;

            for (int k = 0; k < 9; k++)
            {
                keyBusyUntil[k] = -1;
                lastActionTimeOnKey[k] = -999;
            }

            for (int i = 0; i < totalSlots; i++)
            {
                UpdatePatternState(i, division, levelIndex);
                if (centerHoldLockTimer > 0) centerHoldLockTimer--;
                if (touchHoldCooldown > 0) touchHoldCooldown--;
                if (slideCooldown > 0) slideCooldown--;

                if (touchSessionTimer > 0) touchSessionTimer--;
                if (sessionGlobalCooldown > 0) sessionGlobalCooldown--;

                if (skipSlots > 0)
                {
                    sb.Append(",");
                    FormatLine(sb, i, division);
                    skipSlots--;
                    silenceCounter = 0;
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

                double dynamicChance = spawnChance;
                if (chartStyle == 0 && currentVolume < volumeThreshold * 1.5) dynamicChance *= 0.6;
                else if (currentVolume < volumeThreshold * 1.5) dynamicChance *= 0.8;

                bool volumeCheck = (currentVolume > volumeThreshold) && (rnd.NextDouble() < (currentVolume * dynamicChance + 0.1));
                bool shouldSpawn = forceSpawn || volumeCheck;

                if ((isResting && !forceSpawn) || !shouldSpawn)
                {
                    sb.Append(",");
                    FormatLine(sb, i, division);
                    silenceCounter++;
                    continue;
                }

                lastNoteTime = currentTime;

                bool isBreak = (currentVolume > 0.82);
                if (levelIndex == 0 && rnd.NextDouble() > 0.05) isBreak = false;
                bool isEx = !isBreak && allowEx && (rnd.NextDouble() < 0.2);

                string suffix = "";
                if (isBreak) suffix = "b";
                else if (isEx) suffix = "x";

                string mainNote = "";

                int mainPos = GetNextPos(lastPos, forceAdjacent);

                mainPos = GetSmartFreeKey(mainPos, i, sameKeyMinGap);

                if (i - lastActionTimeOnKey[mainPos] < complexSwitchGap)
                {
                    mainPos = (mainPos + 3) % 8 + 1;
                    mainPos = GetSmartFreeKey(mainPos, i, sameKeyMinGap);
                }

                double typeRoll = rnd.NextDouble();
                bool isMainNoteTouchHold = false;
                bool thisIsHoldOrSlide = false;

                if (touchSessionTimer > 0)
                {
                    if (touchSessionIntervalCounter <= 0)
                    {
                        mainNote = GeneratePatternTouch();
                        touchSessionIntervalCounter = (levelIndex >= 3) ? 4 : 8;
                        silenceCounter = 0;
                    }
                    else
                    {
                        sb.Append(",");
                        FormatLine(sb, i, division);
                        touchSessionIntervalCounter--;
                        silenceCounter++;
                        continue;
                    }
                }
                else
                {
                    bool isOutro = (i > totalSlots * 0.90);
                    bool isQuietSection = (currentVolume < volumeThreshold * 0.6);
                    bool hasPreWait = (silenceCounter >= division);
                    bool allowTouchHoldHere = (isOutro || isQuietSection) && hasPreWait;

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
                        // Check if start key was recently used
                        if (i - lastActionTimeOnKey[mainPos] < complexSwitchGap)
                        {
                            mainPos = (mainPos + 3) % 8 + 1;
                            mainPos = GetSmartFreeKey(mainPos, i, complexSwitchGap);
                        }

                        if (mainPos == lastSlideEndPos) mainPos = (mainPos % 8) + 1;
                        mainPos = GetSmartFreeKey(mainPos, i, complexSwitchGap);

                        string slideSuffix = isBreak ? "b" : "";
                        mainNote = GenerateValidSlide(mainPos, allowComplexSlide, slideSuffix, slowSlide, out int slideEnd);

                        bool pathBlocked = IsSlidePathBlocked(mainNote, mainPos, i);

                        if (!pathBlocked)
                        {
                            lastPos = mainPos;
                            lastSlideEndPos = slideEnd;

                            int slideDurationSlots = slowSlide ? (division * 2) : division;
                            MarkSlidePathBusy(mainNote, mainPos, i, slideDurationSlots);
                            slideBusyUntil = i + slideDurationSlots;

                            lastActionTimeOnKey[mainPos] = i;
                            thisIsHoldOrSlide = true;

                            if (levelIndex <= 3) slideCooldown = division * 2;
                            else slideCooldown = division;
                        }
                        else
                        {
                            mainNote = $"{mainPos}{suffix}";
                            lastPos = mainPos;
                            lastActionTimeOnKey[mainPos] = i;
                        }
                    }
                    // 3. Touch
                    else if (pTouch > 0 && typeRoll < (pSlide + pTouch) && i >= slideBusyUntil)
                    {
                        bool isBusy = false;
                        for (int k = 1; k <= 8; k++) if (keyBusyUntil[k] > i) isBusy = true;

                        if (!isBusy)
                        {
                            bool canSpawnSession = (currentSessionCount < maxSessionQuota) && (sessionGlobalCooldown == 0);
                            bool forceCatchUp = (currentSessionCount < minSessionQuota) && (i > totalSlots * 0.6) && (sessionGlobalCooldown == 0);
                            double actualTouchChance = forceCatchUp ? 0.8 : 1.0;

                            if (canSpawnSession && rnd.NextDouble() < actualTouchChance)
                            {
                                mainNote = GenerateImprovedTouch("", lastPos, allowOuterTouch);
                                touchSessionTimer = division * rnd.Next(1, 3);
                                currentSessionCount++;
                                sessionGlobalCooldown = totalSlots / (maxSessionQuota + 2);
                                if (levelIndex >= 3) touchPatternMode = rnd.Next(0, 4);
                                else touchPatternMode = rnd.Next(0, 2);
                                touchPatternStep = rnd.Next(0, 2) == 0 ? 1 : -1;
                                dualToggleType = rnd.Next(0, 2);
                                dualToggleState = false;
                                touchSessionIntervalCounter = (levelIndex >= 3) ? 4 : 8;
                            }
                            else
                            {
                                if (forceSpawn) suffix = "";
                                else if (isBreak) suffix = "b";

                                mainPos = GetSmartFreeKey(mainPos, i, sameKeyMinGap);
                                mainNote = $"{mainPos}{suffix}";
                                lastPos = mainPos;
                                lastActionTimeOnKey[mainPos] = i;
                            }
                        }
                        else
                        {
                            mainPos = GetSmartFreeKey(mainPos, i, sameKeyMinGap);
                            mainNote = $"{mainPos}{suffix}";
                            lastPos = mainPos;
                            lastActionTimeOnKey[mainPos] = i;
                        }
                    }
                    // 4. Hold
                    else if (pHold > 0 && typeRoll < (pSlide + pTouch + pHold))
                    {
                        if (i - lastActionTimeOnKey[mainPos] < complexSwitchGap)
                        {
                            mainPos = (mainPos + 3) % 8 + 1;
                            mainPos = GetSmartFreeKey(mainPos, i, complexSwitchGap);
                        }

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
                            keyBusyUntil[mainPos] = i + holdLen + 2;

                            lastPos = mainPos;
                            skipSlots = holdLen - 1;
                            lastActionTimeOnKey[mainPos] = i;
                            thisIsHoldOrSlide = true;
                        }
                        else
                        {
                            mainNote = $"{mainPos}{suffix}";
                            lastPos = mainPos;
                            lastActionTimeOnKey[mainPos] = i;
                        }
                    }
                    // 5. Tap
                    else
                    {
                        if (forceSpawn) suffix = "";
                        else if (isBreak) suffix = "b";
                        else if (isEx) suffix = "x";

                        mainPos = GetSmartFreeKey(mainPos, i, sameKeyMinGap);
                        mainNote = $"{mainPos}{suffix}";
                        lastPos = mainPos;
                        lastActionTimeOnKey[mainPos] = i;
                    }
                }

                lastTapPos = mainPos;
                lastTapTimeIndex = i;
                silenceCounter = 0;

                // 6. Dual Note
                if (!forceSpawn && allowDual && !isMainNoteTouchHold && rnd.NextDouble() < pDualRate)
                {
                    if (touchSessionTimer > 0)
                    {
                        sb.Append(mainNote);
                    }
                    else
                    {
                        int dualPos;
                        if (mainNote.Contains("C") || mainNote.Contains("B") || mainNote.Contains("E"))
                        {
                            dualPos = (lastPos + 3) % 8 + 1;
                            dualPos = GetSmartFreeKey(dualPos, i, sameKeyMinGap);
                            string dualSuffix = isBreak ? "b" : "";
                            sb.Append($"{mainNote}/{dualPos}{dualSuffix}");
                            if (!dualSuffix.Contains("C")) lastActionTimeOnKey[dualPos] = i;
                        }
                        else
                        {
                            int offset = rnd.Next(0, 2) == 0 ? 4 : 1;
                            dualPos = (lastPos + offset - 1) % 8 + 1;
                            if (dualPos == lastPos) dualPos = (dualPos % 8) + 1;

                            int safeDualPos = -1;

                            for (int attempt = 0; attempt < 3; attempt++)
                            {
                                int tryPos = GetSmartFreeKey(dualPos, i, sameKeyMinGap);
                                int diff = Math.Abs(tryPos - mainPos);
                                if (diff != 0 && diff != 1 && diff != 7)
                                {
                                    safeDualPos = tryPos;
                                    break;
                                }
                                dualPos = (dualPos % 8) + 1;
                            }

                            if (safeDualPos != -1)
                            {
                                string dualSuffix = isBreak ? "b" : (isEx ? "x" : "");
                                if (thisIsHoldOrSlide)
                                    sb.Append($"{mainNote}/{safeDualPos}{dualSuffix}");
                                else
                                    sb.Append($"{mainNote}/{safeDualPos}{dualSuffix}");

                                lastActionTimeOnKey[safeDualPos] = i;
                            }
                            else
                            {
                                sb.Append(mainNote);
                            }
                        }
                    }
                }
                else
                {
                    sb.Append(mainNote);
                }

                sb.Append(",");
                FormatLine(sb, i, division);
            }

            sb.Append("E");
            return sb.ToString();
        }

        private int GetSmartFreeKey(int startPos, int currentIndex, int minGap)
        {
            bool IsSafe(int pos)
            {
                if (keyBusyUntil[pos] > currentIndex) return false;
                if ((currentIndex - lastActionTimeOnKey[pos]) < minGap) return false;
                return true;
            }

            if (IsSafe(startPos)) return startPos;

            int opposite = (startPos + 4 - 1) % 8 + 1;
            if (IsSafe(opposite)) return opposite;

            for (int offset = 1; offset < 8; offset++)
            {
                int checkPos = (startPos + offset - 1) % 8 + 1;
                if (IsSafe(checkPos)) return checkPos;
            }
            return startPos;
        }

        private bool IsSlidePathBlocked(string slideStr, int startPos, int currentIndex)
        {
            List<int> path = CalculateSlidePath(slideStr, startPos);
            foreach (int pos in path)
            {
                if (keyBusyUntil[pos] > currentIndex) return true;
                if ((currentIndex - lastActionTimeOnKey[pos]) < 2) return true;
            }
            return false;
        }

        private void MarkSlidePathBusy(string slideStr, int startPos, int currentTime, int duration)
        {
            List<int> path = CalculateSlidePath(slideStr, startPos);
            int busyUntil = currentTime + duration;
            foreach (int pos in path)
            {
                keyBusyUntil[pos] = busyUntil;
            }
        }

        private List<int> CalculateSlidePath(string slideStr, int startPos)
        {
            List<int> path = new List<int>();
            int endPos = startPos;
            char[] splitChars = new char[] { '-', '^', 'v', 'p', 'q', '>', '<', 's', 'z', 'V' };
            int opIndex = slideStr.IndexOfAny(splitChars);
            char type = (opIndex != -1) ? slideStr[opIndex] : '-';

            if (opIndex != -1)
            {
                if (int.TryParse(slideStr.Substring(opIndex + 1, 1), out int parsedEnd))
                {
                    endPos = parsedEnd;
                }
            }

            path.Add(startPos);
            path.Add(endPos);

            if (type == 'p' || type == 'q' || type == 'v' || type == 'V')
            {
                for (int k = 1; k <= 8; k++) path.Add(k);
            }
            else
            {
                int distCW = (endPos - startPos + 8) % 8;
                int distCCW = (startPos - endPos + 8) % 8;

                if (distCW <= distCCW)
                {
                    for (int k = 1; k < distCW; k++) path.Add((startPos + k - 1) % 8 + 1);
                }
                else
                {
                    for (int k = 1; k < distCCW; k++) path.Add((startPos - k - 1 + 8) % 8 + 1);
                }
            }
            return path;
        }

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
                case 0: int offsetArc = rnd.Next(2, 4); endPos = (start + offsetArc - 1) % 8 + 1; slideStr = $"{start}{suffix}^{endPos}{duration}"; break;
                case 1: endPos = (start + 2) % 8 + 1; slideStr = $"{start}{suffix}>{endPos}{duration}"; break;
                case 2: endPos = (start + 2) % 8 + 1; slideStr = $"{start}{suffix}v{endPos}{duration}"; break;
                case 3: endPos = (start + 4) % 8 + 1; slideStr = $"{start}{suffix}p{endPos}{duration}"; break;
                case 4: endPos = (start + 4) % 8 + 1; slideStr = $"{start}{suffix}q{endPos}{duration}"; break;
            }
            slideEnd = endPos;
            return slideStr;
        }

        private string GeneratePatternTouch()
        {
            int nextPos = lastTouchNum;
            string region = "B";

            switch (touchPatternMode)
            {
                case 0:
                    nextPos = lastTouchNum + touchPatternStep;
                    if (nextPos > 8) nextPos = 1;
                    if (nextPos < 1) nextPos = 8;
                    lastTouchNum = nextPos;
                    return $"{region}{nextPos}";

                case 1:
                    nextPos = (lastTouchNum + 4);
                    if (nextPos > 8) nextPos -= 8;
                    lastTouchNum = nextPos;
                    return $"{region}{nextPos}";

                case 2:
                    nextPos = lastTouchNum + touchPatternStep;
                    if (nextPos > 8) nextPos = 1;
                    if (nextPos < 1) nextPos = 8;
                    lastTouchNum = nextPos;
                    int dualPos = (nextPos + 4);
                    if (dualPos > 8) dualPos -= 8;
                    return $"{region}{nextPos}/{region}{dualPos}";

                case 3:
                    dualToggleState = !dualToggleState;
                    int p1, p2;
                    if (dualToggleType == 0)
                    {
                        if (dualToggleState) { p1 = 1; p2 = 5; }
                        else { p1 = 3; p2 = 7; }
                    }
                    else
                    {
                        if (dualToggleState) { p1 = 2; p2 = 6; }
                        else { p1 = 4; p2 = 8; }
                    }
                    return $"{region}{p1}/{region}{p2}";
            }

            return $"{region}{nextPos}";
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
                    if (lastTouchNum == 9) { roll = 1.0; continue; }
                    lastTouchNum = 9;
                    return selectedTouch;
                }

                int touchPos = rnd.Next(1, 9);
                if (touchPos == lastTapPos) touchPos = (touchPos % 8) + 1;

                string region = rnd.NextDouble() < 0.7 ? "B" : "E";
                lastTouchNum = touchPos;

                return $"{region}{touchPos}{finalSuffix}";
            }
            return $"B{(lastTapPos % 8) + 1}{finalSuffix}";
        }
    }
}