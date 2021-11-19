using NAudio.Wave;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RideMusic
{
    public class Helpers
    {
        public static bool GetKey(Key keyCode)
        {
            return Keyboard.current[keyCode].IsPressed();
        }

        public static bool GetKeyDown(Key keyCode)
        {
            return Keyboard.current[keyCode].wasPressedThisFrame;
        }

        public static bool GetKeyUp(Key keyCode)
        {
            return Keyboard.current[keyCode].wasReleasedThisFrame;
        }

        /// <summary>
        /// This is OWML.ModHelper.Assets.ModAssets.GetAudio(), but with a divisor to apply to the sample count as passed to AudioClip.Create(). See comments.
        /// </summary>
        public static AudioClip GetAudio(string fullPath, double audioClipLengthWorkaroundDivisor)
        {
            /*
                I found that the reported AudioClip.length in seconds of clips created this way was exactly 8 times longer than desired,
                    so an AudioSource would continue playing dead air to fill that time after the samples ran out. Dividing the sample count passed to Create() by 8 compensated for this.
                There are some complaints about similar-sounding issues out there dating back awhile, so this is most likely a bug in Unity 2017.3.
                    It's probably gone mostly unnoticed as it's only a serious problem when playing looped audio.
                I saw the effect with both .mp3 and .wav saved from Audacity and vlc, but haven't traced into the values reported by NAudio in here.
            */
            using (var reader = new AudioFileReader(fullPath))
            {
                var outputBytes = new float[reader.Length];
                reader.Read(outputBytes, 0, (int)reader.Length);
                var clip = AudioClip.Create(fullPath, Convert.ToInt32(((int)reader.Length / audioClipLengthWorkaroundDivisor)), reader.WaveFormat.Channels, reader.WaveFormat.SampleRate, false);
                clip.SetData(outputBytes, 0);
                return clip;
            }
        }
    }
}
