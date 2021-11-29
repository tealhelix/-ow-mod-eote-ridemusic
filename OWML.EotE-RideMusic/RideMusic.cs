using OWML.ModHelper;
using OWML.Common;
using System;
using UnityEngine;
using OWML.Utils;
using UnityEngine.InputSystem;

namespace RideMusic
{
    /// <summary>
    /// Outer Wilds: Echoes of the Eye OWML mod which replaces the music coming from the party house in the first dreamworld area with a custom track.
    /// Keyboard commands also play the same track as ambient music if desired.
    /// </summary>
    public class RideMusic : ModBehaviour
    {
        private AudioClip _rideAudioClip;
        private GameObject _ambientAudioObject;
        private AudioSource _ambientAudioSource;

        private void Start()
        { 
            ModHelper.Events.Subscribe<PartyMusicController>(Events.AfterStart);
            ModHelper.Events.Event += OnEvent;
            
            // This is a workaround for a likely bug in Unity 2017.3, and probably always needs to be 8 for now. See comments in Helpers.GetAudio()
            var audioClipLengthWorkaroundDivisor = ModHelper.Config.GetSettingsValue<double>("audioClipLengthWorkaroundDivisor");
            if (audioClipLengthWorkaroundDivisor <= 0f)
                audioClipLengthWorkaroundDivisor = 1.0f;

            var audioFileName = ModHelper.Config.GetSettingsValue<string>("audioFileName");
            _rideAudioClip = Helpers.GetAudio(ModHelper.Manifest.ModFolderPath + audioFileName, audioClipLengthWorkaroundDivisor);

            // Make sure PartyMusicController now does effectively nothing but start and stop our own sound:

            // ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("Start", typeof(PartyPatches), "Start");
            ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("FadeIn", typeof(PartyPatches), "FadeIn");
            ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("FadeOut", typeof(PartyPatches), "FadeOut");
            ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("StaggerStop", typeof(PartyPatches), "StaggerStop");
            ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("Update", typeof(PartyPatches), "Update");

            OWTime.OnPause += OnPause;
            OWTime.OnUnpause += OnUnpause;

            // ModHelper.Console.WriteLine("Avast, 'Echoes of the Caribbean' be loaded.", MessageType.Success);
        }

        private void OnPause(OWTime.PauseType pauseType)
        {
            if (pauseType == OWTime.PauseType.Menu)
            {
                PartyPatches.s_FaderAudioSource._audioSource.Pause();
                _ambientAudioSource.Pause();
            }
        }

        private void OnUnpause(OWTime.PauseType pauseType)
        {
            if (pauseType == OWTime.PauseType.Menu)
            {
                PartyPatches.s_FaderAudioSource._audioSource.UnPause();
                _ambientAudioSource.UnPause();
            }
        }

        private AudioRolloffMode GetRolloffMode()
        {
            var rolloffName = ModHelper.Config.GetSettingsValue<string>("audioRolloffMode").Trim();
            if (Enum.IsDefined(typeof(AudioRolloffMode), rolloffName))
                return (AudioRolloffMode)Enum.Parse(typeof(AudioRolloffMode), rolloffName);
            else
            {
                ModHelper.Console.WriteLine("AudioRolloffMode parse failed from config value: " + rolloffName);
                return AudioRolloffMode.Custom;
            }
        }

        private void OnEvent(MonoBehaviour behaviour, Events ev)
        {

            switch (behaviour)
            {
                case PartyMusicController partyMusicController
                    when ev == Events.AfterStart:

                    // The PartyMusicController is a behavior Component attached to a GameObject sitting in our friends' party house.
                    // They're actually playing four instruments: a lead, a drone, vocals, and bass. When you enter the house (StaggerStop() called) these fade rapidly and at different times.
                    // We're going to suppress all that and play our AudioSource from the house object instead.

                    // After a gameplay loop ending, all this stuff gets reset but the original audio clips aren't reloaded.

                    var owAudioSources = partyMusicController.GetValue<OWAudioSource[]>("_instrumentSources"); // array of 4

                    // The component has to be on the party object too to get the spatial effects.
                    var newAudioSource = partyMusicController.gameObject.AddComponent<AudioSource>();
                    newAudioSource.clip = _rideAudioClip;
                    newAudioSource.playOnAwake = false;
                    newAudioSource.spatialBlend = 1f; // could also get spatial lowpass filter in a mixer?
                    newAudioSource.dopplerLevel = 0;
                    newAudioSource.loop = true;
                    newAudioSource.maxDistance = ModHelper.Config.GetSettingsValue<int>("soundMaxDistance");

                    // The original custom curve looks logarithmic but closer to AudioRolloffMode.Linear than the fairly drastic AudioRolloffMod.Logarithmic
                    newAudioSource.rolloffMode = GetRolloffMode();
                    if (newAudioSource.rolloffMode == AudioRolloffMode.Custom)
                        newAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, owAudioSources[0].GetCustomCurve(AudioSourceCurveType.CustomRolloff));

                    PartyPatches.s_FaderAudioSource._audioSource = newAudioSource;

                    // For the ambient source we don't need spatial effects so create object with arbitrary location, and hang on to it as extra protection against disposal
                    _ambientAudioObject = new GameObject(); 
                    _ambientAudioSource = _ambientAudioObject.AddComponent<AudioSource>();
                    _ambientAudioSource.clip = _rideAudioClip;
                    _ambientAudioSource.volume = 0;
                    _ambientAudioSource.loop = true;
                    _ambientAudioSource.playOnAwake = false;

                    // Unity just did not want to stop playing the instruments, and only modifying them (e.g. replacing _audioSource.clip) and even nulling references didn't work.
                    // So, we're going to stomp on their handcrafted wooden instruments with extreme prejudice.

                    // Suppress the AudioSources
                    foreach (var modSource in owAudioSources)
                    {
                        modSource.loop = false;
                        modSource.Stop();
                        if (modSource.clip != null)
                        {
                            // only on first gameplay loop
                            modSource.clip.UnloadAudioData();   // this is probably all the stomping really needed
                            Destroy(modSource.clip);            // but why not
                        }
                    }

                    // And hand back a sad array of smoking nothingness
                    partyMusicController.SetValue("_instrumentSources", new OWAudioSource[0]);
                    break;
            }
        }

        private void Update()
        {
            PartyPatches.s_FaderAudioSource.UpdateFade();

            // Playback of the same track as ambient music
            if (Helpers.GetKeyDown(Key.Slash) && Helpers.GetKey(Key.Comma))
            {
                // Ambient track volume up / play
                if (!_ambientAudioSource.isPlaying)
                {
                    if (PartyPatches.s_FaderAudioSource._audioSource.isPlaying)
                        _ambientAudioSource.time = PartyPatches.s_FaderAudioSource._audioSource.time;
                    _ambientAudioSource.Play();
                }
                _ambientAudioSource.volume += 0.05f;
                if (_ambientAudioSource.volume > 1.0f)
                    _ambientAudioSource.volume = 1.0f;
            }
            else if (Helpers.GetKeyDown(Key.Period) && Helpers.GetKey(Key.Comma))
            {
                // Ambient track volume down / stop
                if (_ambientAudioSource.volume > 0f)
                {
                    _ambientAudioSource.volume -= 0.05f;
                    if (_ambientAudioSource.volume <= 0f)
                    {
                        _ambientAudioSource.volume = 0f;
                        _ambientAudioSource.Stop();
                    }
                }
            }
        }
        
    }
}
