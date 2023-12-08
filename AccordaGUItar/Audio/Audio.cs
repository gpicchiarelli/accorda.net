﻿using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Dsp;
using NAudio.Wave;

namespace AccordaGUItar.Audio
{
    public class Audio
    {
        private readonly WaveInEvent waveIn;
        private const int sampleRate = 44100;
        private const int bufferSize = 2048;
        private readonly float[] buffer;
        private readonly Complex[] complexBuffer;
        private readonly BiQuadFilter filter;

        // Evento per notificare la frequenza media calcolata
        public event EventHandler<double> SmoothedFrequencyDetected;

        // Aggiunto un threshold per il volume minimo rilevabile
        private double volumeThreshold = 0.03;

        public Audio(int InputDeviceSelector = 0)
        {
            waveIn = new WaveInEvent
            {
                DeviceNumber = InputDeviceSelector,
                BufferMilliseconds = bufferSize * 1000 * 2 / sampleRate,
                WaveFormat = new WaveFormat(sampleRate, 1)
            };

            filter = BiQuadFilter.LowPassFilter(44100, 20000, 1);

            buffer = new float[bufferSize];
            complexBuffer = new Complex[bufferSize];
            waveIn.DataAvailable += WaveIn_DataAvailable;
            StartRecording();
        }
        private double CalculateMagnitude(Complex complex)
        {
            return Math.Sqrt(complex.X * complex.X + complex.Y * complex.Y);
        }

        private Queue<double> recentFrequencies = new Queue<double>();
        private int windowSize = 20; // Dimensione della finestra mobile

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            double maxVolume = CalculateMaxVolume(e);
            if (e.Buffer.Length >= 2 * bufferSize -1)
            {
                if (maxVolume > volumeThreshold)
                {
                    for (int i = 0; i < bufferSize; i++)
                    {
                        short sample = (short)(e.Buffer[2 * i + 1] << 8 | e.Buffer[2 * i]);
                        buffer[i] = (float)sample / short.MaxValue;
                        buffer[i] = filter.Transform(buffer[i]);
                        complexBuffer[i].X = buffer[i];
                        complexBuffer[i].Y = 0;
                    }

                    FastFourierTransform.FFT(true, (int)Math.Log(bufferSize, 2.0), complexBuffer);

                    int maxIndex = 0;
                    double maxMagnitude = 0;

                    for (int i = 0; i < bufferSize / 2; i++)
                    {
                        double magnitude = CalculateMagnitude(complexBuffer[i]);
                        if (magnitude > maxMagnitude)
                        {
                            maxMagnitude = magnitude;
                            maxIndex = i;
                        }
                    }

                    double frequency = maxIndex * sampleRate / bufferSize;
                    SmoothedFrequencyDetected?.Invoke(this, determinaFrequenzaMisurata(frequency));
                }
                else 
                {
                    SmoothedFrequencyDetected?.Invoke(this, determinaFrequenzaMisurata(0));
                }
            }
        }

        private double determinaFrequenzaMisurata(double frequenzaRilevata) 
        {
            recentFrequencies.Enqueue(frequenzaRilevata);
            if (recentFrequencies.Count > windowSize)
            {
                recentFrequencies.Dequeue(); // Rimuovi il campione più vecchio se la finestra è piena
            }
            double averageFrequency = recentFrequencies.Average();

            double weightedSum = 0.0;
            double weightSum = 0.0;
            int position = 0;
            foreach (double freq in recentFrequencies)
            {
                double weight = 1.0 / Math.Pow(2, position); // Assegna un peso decrescente ai campioni
                weightedSum += freq * weight;
                weightSum += weight;
                position++;
            }

            double weightedAverageFrequency = weightedSum / weightSum;
            return weightedAverageFrequency;
        }

        private double CalculateMaxVolume(WaveInEventArgs e)
        {
            double maxVolume = 0.0;

            for (int i = 0; i < e.BytesRecorded / 2; i++)
            {
                short sample = (short)(e.Buffer[2 * i + 1] << 8 | e.Buffer[2 * i]);
                double normalizedSample = (double)sample / short.MaxValue;
                double volume = Math.Abs(normalizedSample);

                if (volume > maxVolume)
                {
                    maxVolume = volume;
                }
            }

            return maxVolume;
        }

        public List<string> ElencaDispositiviIngresso()
        {
            int inputDeviceCount = WaveInEvent.DeviceCount;
            List<string> dispositivi = [];
            for (int deviceIndex = 0; deviceIndex < inputDeviceCount; deviceIndex++)
            {
                WaveInCapabilities deviceInfo = WaveInEvent.GetCapabilities(deviceIndex);
                dispositivi.Add($"Dispositivo {deviceIndex + 1}: {deviceInfo.ProductName}");
            }
            return dispositivi;
        }

        private void StartRecording()
        {
            waveIn.StartRecording();
        }

        public void StopRecording()
        {
            waveIn.StopRecording();
        }

        // Aggiunto un metodo per impostare il threshold di volume
        public void SetVolumeThreshold(double threshold)
        {
            volumeThreshold = threshold;
        }
    }
}