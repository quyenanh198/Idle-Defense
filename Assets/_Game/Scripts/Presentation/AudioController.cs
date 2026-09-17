using System;
using System.Collections.Generic;
using IdleHeroDefense.Domain;
using IdleHeroDefense.Infrastructure;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class AudioController : MonoBehaviour
    {
        private readonly List<AudioClip> generatedClips = new List<AudioClip>();
        private AudioSource musicSource;
        private AudioSource sfxSource;
        private AudioClip hitClip;
        private AudioClip ultimateClip;
        private AudioClip victoryClip;
        private AudioClip defeatClip;
        private BattleRunner runner;
        private FeedbackService feedback;

        private void Awake()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0.16f;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = 0.45f;
            musicSource.clip = CreateTone("MusicPlaceholder", new[] { 220f, 277f, 330f }, 4f, 0.035f);
            hitClip = CreateTone("HitPlaceholder", new[] { 130f }, 0.08f, 0.22f);
            ultimateClip = CreateTone("UltimatePlaceholder", new[] { 440f, 660f }, 0.28f, 0.18f);
            victoryClip = CreateTone("VictoryPlaceholder", new[] { 523f, 659f, 784f }, 0.55f, 0.15f);
            defeatClip = CreateTone("DefeatPlaceholder", new[] { 220f, 165f }, 0.55f, 0.15f);
        }

        private void Start()
        {
            AttachRunner(FindObjectOfType<BattleRunner>());
            if (ProfileController.Instance != null)
                feedback = new FeedbackService(ProfileController.Instance.Profile, new UnityHapticProvider());
            ApplyPreferences();
        }

        private void Update()
        {
            if (runner == null) AttachRunner(FindObjectOfType<BattleRunner>());
            ApplyPreferences();
        }

        private void ApplyPreferences()
        {
            var profile = ProfileController.Instance?.Profile;
            if (profile == null) return;
            musicSource.mute = !profile.musicEnabled;
            sfxSource.mute = !profile.soundEnabled;
            if (profile.musicEnabled && !musicSource.isPlaying) musicSource.Play();
            if (!profile.musicEnabled && musicSource.isPlaying) musicSource.Pause();
        }

        private void AttachRunner(BattleRunner candidate)
        {
            if (candidate == null || candidate == runner) return;
            if (runner != null) { runner.DamagePresented -= OnDamage; runner.StatePresented -= OnState; }
            runner = candidate;
            runner.DamagePresented += OnDamage;
            runner.StatePresented += OnState;
        }

        private void OnDamage(DamageEvent damage)
        {
            if (damage.Amount <= 0) return;
            sfxSource.PlayOneShot(damage.IsUltimate ? ultimateClip : hitClip);
            if (damage.IsUltimate) feedback?.TryUltimatePulse();
        }

        private void OnState(BattleState state)
        {
            if (state == BattleState.Victory) sfxSource.PlayOneShot(victoryClip);
            else if (state == BattleState.Defeat) sfxSource.PlayOneShot(defeatClip);
        }

        private AudioClip CreateTone(string name, IReadOnlyList<float> frequencies, float duration, float amplitude)
        {
            const int sampleRate = 22050;
            var samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var time = (float)i / sampleRate;
                var envelope = Mathf.Min(1f, i / (sampleRate * 0.02f)) * Mathf.Clamp01((samples - i) / (sampleRate * 0.08f));
                var value = 0f;
                for (var f = 0; f < frequencies.Count; f++) value += Mathf.Sin(2f * Mathf.PI * frequencies[f] * time);
                data[i] = value / frequencies.Count * amplitude * envelope;
            }
            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            generatedClips.Add(clip);
            return clip;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) musicSource.Pause();
            else ApplyPreferences();
        }

        private void OnDestroy()
        {
            if (runner != null) { runner.DamagePresented -= OnDamage; runner.StatePresented -= OnState; }
            foreach (var clip in generatedClips) if (clip != null) Destroy(clip);
        }
    }
}

