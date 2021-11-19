using UnityEngine;

namespace RideMusic
{
	/// <summary>
	/// Basic AudioSource fader derived from OWAudioSource.
	/// Like OWAudioSource it has-a AudioSource, not is-a.
	/// </summary>
    public class FaderAudioSource
    {
		public AudioSource _audioSource;
		public AudioSource _syncToAudioSource;

		public bool _isFading = false;
		public float _initFadeVolume;
		public float _targetFadeVolume;
		public float _fadeDuration;
		public float _initFadeTime;
		public float _fadeFraction;

		public void FadeTo(float targetVolume, float fadeDuration)
		{
			if (_audioSource.isActiveAndEnabled && !_audioSource.isPlaying && targetVolume > 0f)
			{
				if (_syncToAudioSource && _syncToAudioSource.isPlaying)
					_audioSource.time = _syncToAudioSource.time;
				_audioSource.Play();
			}

			_initFadeVolume = _audioSource.volume;
			_targetFadeVolume = targetVolume;
			_fadeDuration = fadeDuration;
			_initFadeTime = Time.unscaledTime;
			_fadeFraction = 0f;
			_isFading = true;
		}

		public void UpdateFade()
		{
			if (!_isFading)
				return;

			if (_fadeFraction < 1f)
			{
				_fadeFraction = Mathf.InverseLerp(_initFadeTime, _initFadeTime + _fadeDuration, Time.unscaledTime);
				_audioSource.volume = Mathf.Lerp(_initFadeVolume, _targetFadeVolume, Mathf.SmoothStep(0f, 1f, _fadeFraction));
				return;
			}
			if (_targetFadeVolume <= 0f)
			{
				_audioSource.Stop();
			}
			_isFading = false;
		}
	}
}
