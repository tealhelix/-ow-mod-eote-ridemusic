using OWML.ModHelper;
using OWML.Common;
using System;
using UnityEngine;
using OWML.Utils;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace RideMusic
{
    /// <summary>
    /// Outer Wilds: Echoes of the Eye OWML mod which replaces the music coming from the party house in the first dreamworld area with a custom track.
    /// Keyboard commands also play the same track as ambient music if desired.
    /// </summary>
    public class RideMusic : ModBehaviour
    {
        private AudioClip _rideAudioClip;
        private AudioClip _endOfLineAudioClip;
        private GameObject _ambientAudioObject;
        private AudioSource _ambientAudioSource;

        private Light _partySpot;
        private List<Light> _candles = new List<Light>();
        private Light _fireplaceLight;

        private Sequence _interiorColorBeats = new Sequence();
        private Sequence _floodColorBeats = new Sequence();
        private Sequence _floodIntensityBeats = new Sequence();
        private float _lastPlaybackTime;
        private System.Random _sysRand = new System.Random();

        private float _soundMaxDistance;
        private bool _endOfLineMode = false;
        private double _audioClipLengthWorkaroundDivisor;
        private float _beatsInEolTrack;
        private string _rideFileName;

        private void Start()
        {
            // wee faster than every half second
            _beatsInEolTrack = 96;

            _interiorColorBeats._beats = new List<float[]>() { new float[] { 0 }, new float[] { 8 }, new float[] { 16 }, new float[] { 24 } };
            for (var i = 32; i < _beatsInEolTrack; i++)
                _interiorColorBeats._beats.Add(new float[] { i });
            for (var i = 0; i < _beatsInEolTrack; i += 8)
                _floodColorBeats._beats.Add(new float[] { i });
            for (var i = 0; i < 32; i += 8)
            {
                _floodIntensityBeats._beats.Add(new float[] { i, 5.0f });
                _floodIntensityBeats._beats.Add(new float[] { i + 0.25f, 1.0f });
            }
            for (var i = 32; i < 64-7; i += 1)
            {
                _floodIntensityBeats._beats.Add(new float[] { i, 8.0f });
                _floodIntensityBeats._beats.Add(new float[] { i + 0.125f, 0.5f });
            }
            for (var i = 64; i < 96 - 7; i += 1)
            {
                _floodIntensityBeats._beats.Add(new float[] { i, 8.0f });
                _floodIntensityBeats._beats.Add(new float[] { i + 0.125f, 0.5f });
            }

            ModHelper.Events.Subscribe<PartyMusicController>(Events.AfterStart);
            ModHelper.Events.Subscribe<GhostPartyDirector>(Events.AfterStart);
            ModHelper.Events.Event += OnEvent;
            
            _endOfLineAudioClip = Helpers.GetAudio(ModHelper.Manifest.ModFolderPath + "Daft_Punk_Derezzed_party_loop_wet.mp3", _audioClipLengthWorkaroundDivisor);
            // Make sure PartyMusicController now does effectively nothing but start and stop our own sound:

            // ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("Start", typeof(PartyPatches), "Start");
            ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("FadeIn", typeof(PartyPatches), "FadeIn");
            ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("FadeOut", typeof(PartyPatches), "FadeOut");
            ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("StaggerStop", typeof(PartyPatches), "StaggerStop");
            ModHelper.HarmonyHelper.AddPrefix<PartyMusicController>("Update", typeof(PartyPatches), "Update");

            OWTime.OnPause += OnPause;
            OWTime.OnUnpause += OnUnpause;

            //ModHelper.Console.WriteLine("Avast, 'Echoes of the Caribbean' be loaded.", MessageType.Success);
        }
        public override void Configure(IModConfig lconfig)
        {
            // This is a workaround for a likely bug in Unity 2017.3, and probably always needs to be 8 for now. See comments in Helpers.GetAudio()
            _audioClipLengthWorkaroundDivisor = lconfig.GetSettingsValue<double>("audioClipLengthWorkaroundDivisor");
            if (_audioClipLengthWorkaroundDivisor <= 0f)
                _audioClipLengthWorkaroundDivisor = 1.0f;

            var newRideFileName = lconfig.GetSettingsValue<string>("audioFileName");
            if (newRideFileName != _rideFileName)
            {
                _rideAudioClip = Helpers.GetAudio(ModHelper.Manifest.ModFolderPath + newRideFileName, _audioClipLengthWorkaroundDivisor);
                _rideFileName = newRideFileName;
                SwitchMode(_endOfLineMode); // pick up new clip
            }

            _soundMaxDistance = lconfig.GetSettingsValue<int>("soundMaxDistance");

            var newEndOfLineMode = lconfig.GetSettingsValue<bool>("End of Line mode");
            if (newEndOfLineMode != _endOfLineMode)
            {
                SwitchMode(newEndOfLineMode);
                _endOfLineMode = newEndOfLineMode;
            }
        }

        private void ResetAudio(AudioSource source, bool endOfLineMode, float maxDistance)
        {
            var wasPlaying = source.isPlaying;
            var volume = source.volume;
            if (maxDistance > 0f)
                source.maxDistance = maxDistance;
            source.clip = endOfLineMode ? _endOfLineAudioClip : _rideAudioClip;
            if (wasPlaying)
            {
                source.Stop();
                source.volume = volume;
                source.Play();
            }
        }

        private void SwitchMode(bool endOfLineMode)
        {
            var partySource = PartyPatches.s_FaderAudioSource._audioSource;
            if (partySource != null)
            {
                ResetAudio(partySource, endOfLineMode, endOfLineMode ? _soundMaxDistance + 20 : _soundMaxDistance);
                /*
                var volume = partySource.volume;
                var wasPlaying = partySource.isPlaying;
                partySource.maxDistance = endOfLineMode ? _soundMaxDistance + 20 : _soundMaxDistance;
                partySource.clip = endOfLineMode ? _endOfLineAudioClip : _rideAudioClip;
                if (wasPlaying)
                {
                    partySource.Stop();
                    partySource.Play();
                    partySource.volume = volume;
                }
                */
            }
            if (_ambientAudioSource != null)
            {
                ResetAudio(_ambientAudioSource, endOfLineMode, 0f);
/*
                var volume = _ambientAudioSource.volume;
                var wasPlaying = _ambientAudioSource.isPlaying;
                ModHelper.Console.WriteLine($"volume {volume}");
                _ambientAudioSource.clip = endOfLineMode ? _endOfLineAudioClip : _rideAudioClip;
                if (wasPlaying)
                {
                    _ambientAudioSource.Stop();
                    _ambientAudioSource.Play();
                    _ambientAudioSource.volume = volume;
                }
*/
            }
        }

        private void OnPause(OWTime.PauseType pauseType)
        {
            if (pauseType == OWTime.PauseType.Menu)
            {
                if (PartyPatches.s_FaderAudioSource._audioSource != null)
                    PartyPatches.s_FaderAudioSource._audioSource.Pause();
                if (_ambientAudioSource != null)
                    _ambientAudioSource.Pause();
            }
        }

        private void OnUnpause(OWTime.PauseType pauseType)
        {
            if (pauseType == OWTime.PauseType.Menu)
            {
                if (PartyPatches.s_FaderAudioSource._audioSource != null)
                    PartyPatches.s_FaderAudioSource._audioSource.UnPause();
                if (_ambientAudioSource != null)
                    _ambientAudioSource.UnPause();
            }
        }

        private AudioRolloffMode GetRolloffMode()
        {
            return AudioRolloffMode.Custom;
            /*
             * I felt like there was too much in config, dropped this
            var rolloffName = ModHelper.Config.GetSettingsValue<string>("audioRolloffMode").Trim();
            if (Enum.IsDefined(typeof(AudioRolloffMode), rolloffName))
                return (AudioRolloffMode)Enum.Parse(typeof(AudioRolloffMode), rolloffName);
            else
            {
                ModHelper.Console.WriteLine("AudioRolloffMode parse failed from config value: " + rolloffName);
                return AudioRolloffMode.Custom;
            }
            */
        }

        private void OnEvent(MonoBehaviour behaviour, Events ev)
        {

            switch (behaviour)
            {
                case GhostPartyDirector ghostPartyDirector
                    when ev == Events.AfterStart:

                    // spotlight shining right out of the fireplace ghost!
                    var fireplaceBrain = ghostPartyDirector.GetValue<GhostBrain>("_fireplaceGhost");
                    _fireplaceLight = fireplaceBrain.gameObject.AddComponent<Light>();
                    _fireplaceLight.type = LightType.Spot;
                    _fireplaceLight.range = 30;
                    _fireplaceLight.intensity = 4.0f;
                    _fireplaceLight.spotAngle = 25f; // was 30f
                    _fireplaceLight.innerSpotAngle = 25f; // was 21.80208
                    //_fireplaceLight.lightShadowCasterMode = LightShadowCasterMode.Everything;
                    break;

                case PartyMusicController partyMusicController
                    when ev == Events.AfterStart:

                    // The PartyMusicController is a behavior Component attached to a GameObject sitting in our friends' party house.
                    // They're actually playing four instruments: a lead, a drone, vocals, and bass. When you enter the house (StaggerStop() called) these fade rapidly and at different times.
                    // We're going to suppress all that and play our AudioSource from the house object instead.

                    // After a gameplay loop ending, all this stuff gets reset but the original audio clips aren't reloaded.
                    
                    var owAudioSources = partyMusicController.GetValue<OWAudioSource[]>("_instrumentSources"); // array of 4

                    // The component has to be on the party object too to get the spatial effects.
                    var newAudioSource = partyMusicController.gameObject.AddComponent<AudioSource>();
                    newAudioSource.clip = _endOfLineMode ? _endOfLineAudioClip : _rideAudioClip;
                    newAudioSource.playOnAwake = false;
                    newAudioSource.spatialBlend = 1f; // could also get spatial lowpass filter in a mixer?
                    newAudioSource.dopplerLevel = 0;
                    newAudioSource.loop = true;
                    newAudioSource.maxDistance = _endOfLineMode ? _soundMaxDistance + 20 : _soundMaxDistance;

                    // The original custom curve looks logarithmic but closer to AudioRolloffMode.Linear than the fairly drastic AudioRolloffMod.Logarithmic
                    newAudioSource.rolloffMode = GetRolloffMode();
                    if (newAudioSource.rolloffMode == AudioRolloffMode.Custom)
                        newAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, owAudioSources[0].GetCustomCurve(AudioSourceCurveType.CustomRolloff));

                    PartyPatches.s_FaderAudioSource._audioSource = newAudioSource;

                    // For the ambient source we don't need spatial effects so create object with arbitrary location, and hang on to it as extra protection against disposal
                    _ambientAudioObject = new GameObject(); 
                    _ambientAudioSource = _ambientAudioObject.AddComponent<AudioSource>();
                    _ambientAudioSource.clip = _endOfLineMode ? _endOfLineAudioClip : _rideAudioClip;
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

                    _lastPlaybackTime = 0;
                    break;
            }
        }

        public Color[] _lightColors = new Color[] { new Color(1, 0, 0), new Color(0, 1, 0), new Color(0, 0, 1),
                                                    new Color(1, 1, 0), new Color(1, 0, 1), new Color(0, 1, 1), new Color(1, 1, 1) };


        private Color RandColor(Color avoidColor)
        {
            int randIndex;
            do
            {
                randIndex = _sysRand.Next(_lightColors.Length);
            } while (_lightColors[randIndex] == avoidColor);

            return _lightColors[randIndex];
            /*
            var color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            var max = color.maxColorComponent;
            color.r /= max;
            color.g /= max;
            color.b /= max;
            return color;
            */
        }

        private void Update()
        {
            if (_endOfLineMode && PartyPatches.s_FaderAudioSource._audioSource != null && PartyPatches.s_FaderAudioSource._audioSource.isPlaying)
            {
                if (_partySpot == null)
                {
                    // lazy load lights
                    // spotlight under the house shining upward
                    var partySpotObj = GameObject.Find("Spotlight_partyhouse");
                    _partySpot = partySpotObj.GetComponent<Light>();

                    // 8 candles in rafters, their intensity is controlled by a LightFlicker2 class
                    for (int i = 1; i <= 8; i++)
                        _candles.Add((GameObject.Find($"PointLight_Candle_Large ({i})")).GetComponent<Light>());
                }

                float playbackTime = PartyPatches.s_FaderAudioSource._audioSource.time * (_beatsInEolTrack / PartyPatches.s_FaderAudioSource._audioSource.clip.length);
                if (_lastPlaybackTime > playbackTime)
                {
                    _lastPlaybackTime = 0; // reset loop
                    _interiorColorBeats.Reset();
                    _floodColorBeats.Reset();
                    _floodIntensityBeats.Reset();
                }

                // the roof is on fire
                var interior = _interiorColorBeats.Next(_lastPlaybackTime, playbackTime);
                if (interior != null)
                {
                    foreach (var candle in _candles)
                        candle.color = RandColor(candle.color);
                    _fireplaceLight.color = RandColor(_fireplaceLight.color);
                }
                var floodColor = _floodColorBeats.Next(_lastPlaybackTime, playbackTime);
                if (floodColor != null)
                    _partySpot.color = RandColor(_partySpot.color);

                var floodIntensity = _floodIntensityBeats.Next(_lastPlaybackTime, playbackTime);
                if (floodIntensity != null)
                    _partySpot.intensity = floodIntensity[1];

                _lastPlaybackTime = playbackTime;
            }

            PartyPatches.s_FaderAudioSource.UpdateFade();

            if (Helpers.GetKeyDown(Key.Semicolon) && Helpers.GetKey(Key.Comma))
            {
                if (! _endOfLineMode)
                {
                    SwitchMode(true);
                    _endOfLineMode = true;
                    ModHelper.Config.SetSettingsValue("End of Line mode", true);
                }
            }
            else if (Helpers.GetKeyDown(Key.Quote) && Helpers.GetKey(Key.Comma))
            {
                if (_endOfLineMode)
                {
                    SwitchMode(false);
                    _endOfLineMode = false;
                    ModHelper.Config.SetSettingsValue("End of Line mode", false);
                }
            }

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
