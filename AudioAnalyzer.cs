using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace SiMaiGenerator
{
    public class AudioAnalyzer
    {
        public float[] WaveformData { get; private set; }
        public int SampleRate { get; private set; }
        public double TotalSeconds { get; private set; }

        /// <param name="filePath"></param>
        public void LoadAudio(string filePath)
        {
            using (var reader = new AudioFileReader(filePath))
            {
                SampleRate = reader.WaveFormat.SampleRate;
                TotalSeconds = reader.TotalTime.TotalSeconds;

                var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
                var samples = new List<float>();
                int samplesRead;

                while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < samplesRead; i += reader.WaveFormat.Channels)
                    {
                        float sum = 0;
                        for (int ch = 0; ch < reader.WaveFormat.Channels; ch++)
                        {
                            if (i + ch < samplesRead)
                            {
                                sum += buffer[i + ch];
                            }
                        }
                        samples.Add(sum / reader.WaveFormat.Channels);
                    }
                }

                WaveformData = samples.ToArray();
            }
        }

        public int DetectBPM()
        {
            if (WaveformData == null || WaveformData.Length == 0) return 120; 

            int windowSize = 1024; 
            List<float> energyHistory = new List<float>();

            for (int i = 0; i < WaveformData.Length; i += windowSize)
            {
                float sum = 0;
                int count = 0;
                for (int j = 0; j < windowSize && i + j < WaveformData.Length; j++)
                {
                    sum += WaveformData[i + j] * WaveformData[i + j]; 
                    count++;
                }
                float rms = (float)Math.Sqrt(sum / count); 
                energyHistory.Add(rms);
            }

            List<int> beatIndices = new List<int>();
            int historyBuffer = 43;

            for (int i = historyBuffer; i < energyHistory.Count; i++)
            {
                float localAverage = 0;
                for (int h = 1; h <= historyBuffer; h++)
                {
                    localAverage += energyHistory[i - h];
                }
                localAverage /= historyBuffer;


                if (energyHistory[i] > localAverage * 1.3 && energyHistory[i] > 0.05)
                {
                    if (beatIndices.Count == 0 || (i - beatIndices[beatIndices.Count - 1]) > 10)
                    {
                        beatIndices.Add(i);
                    }
                }
            }

            if (beatIndices.Count < 10) return 120;

            Dictionary<int, int> intervalCounts = new Dictionary<int, int>();

            for (int i = 1; i < beatIndices.Count; i++)
            {

                int interval = beatIndices[i] - beatIndices[i - 1];

                double windowTime = (double)windowSize / SampleRate;
                double seconds = interval * windowTime;
                double bpmEstimate = 60.0 / seconds;


                while (bpmEstimate < 60) bpmEstimate *= 2; 
                while (bpmEstimate > 200) bpmEstimate /= 2; 

                int roundedBpm = (int)Math.Round(bpmEstimate);

                if (intervalCounts.ContainsKey(roundedBpm))
                    intervalCounts[roundedBpm]++;
                else
                    intervalCounts[roundedBpm] = 1;
            }

            var sortedBpm = intervalCounts.OrderByDescending(x => x.Value).ToList();

            if (sortedBpm.Count > 0)
            {
                return sortedBpm[0].Key;
            }

            return 120;
        }


        public float GetVolumeAt(double time, double window = 0.1)
        {
            if (WaveformData == null) return 0;

            int startSample = (int)(time * SampleRate);
            int sampleCount = (int)(window * SampleRate);

            if (startSample < 0 || startSample >= WaveformData.Length) return 0;
            if (startSample + sampleCount > WaveformData.Length)
                sampleCount = WaveformData.Length - startSample;

            float sum = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                float val = WaveformData[startSample + i];
                sum += val * val;
            }

            float rms = (float)Math.Sqrt(sum / sampleCount);


            return Math.Min(1.0f, rms * 3.0f);
        }

        public string GetDebugInfo()
        {
            if (WaveformData == null) return "No data";
            return $"Sampling rate: {SampleRate} Hz\n" +
                   $"Total duration: {TotalSeconds:F2} Seconds\n" +
                   $"Total sample size: {WaveformData.Length}";
        }
    }


}