#if VFX_HAS_TIMELINE
using UnityEngine;
using UnityEngine.Playables;

namespace UnityEngine.VFX
{
    public class VisualEffectControlTrackMixerBehaviour : PlayableBehaviour
    {
        string m_DefaultText;
        VisualEffect m_Target;
        bool[] enabledStates;



        class ScrubbingCacheHelper
        {
            struct Event
            {
                public enum Type
                {
                    Play,
                    Stop
                }
                public Type type;
                public double time;
            }

            struct Chunk
            {
                public double begin;
                public double end;
                public VisualEffectControlPlayableBehaviour[] playables;
            }

            public void Init(Playable playable)
            {
                int inputCount = playable.GetInputCount();
                for (int i = 0; i < inputCount; ++i)
                {
                    var clip = playable.GetInput(i);
                    var a = clip.GetDuration();
                    var b = clip.GetTime();
                    ScriptPlayable<VisualEffectControlPlayableBehaviour> inputPlayable = (ScriptPlayable<VisualEffectControlPlayableBehaviour>)playable.GetInput(i);

                    var c = inputPlayable.GetBehaviour();

                    Debug.Log(a + "; " + b + " ; " + c.clipStart);
                }
            }
        }

        ScrubbingCacheHelper m_ScrubbingCacheHelper;
        public override void PrepareFrame(Playable playable, FrameData data)
        {
            if (m_ScrubbingCacheHelper == null)
            {
                m_ScrubbingCacheHelper = new ScrubbingCacheHelper();
                m_ScrubbingCacheHelper.Init(playable);
            }

            int inputCount = playable.GetInputCount();

            var time = (float)playable.GetTime();
            //Debug.Log(time);
        }

        // Called every frame that the timeline is evaluated. ProcessFrame is invoked after its' inputs.
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            SetDefaults(playerData as VisualEffect);
            if (m_Target == null)
                return;

            int inputCount = playable.GetInputCount();

            float totalWeight = 0f;
            float greatestWeight = 0f;
            string text = m_DefaultText;

            //TODOPAUL : Focus a bit more on this code
            int playableIndex = 0;
            for (int i = 0; i < inputCount; i++)
            {
                float inputWeight = playable.GetInputWeight(i);
                ScriptPlayable<VisualEffectControlPlayableBehaviour> inputPlayable = (ScriptPlayable<VisualEffectControlPlayableBehaviour>)playable.GetInput(i);
                VisualEffectControlPlayableBehaviour input = inputPlayable.GetBehaviour();

                totalWeight += inputWeight;

                // use the text with the highest weight
                if (inputWeight > greatestWeight)
                {
                    text = input.text;
                    greatestWeight = inputWeight;
                    playableIndex = 0;
                }
            }

            bool wasEnabled = m_Target.enabled;
            m_Target.enabled = greatestWeight > 0.0f;
            if (!wasEnabled && m_Target.enabled)
            {
                //Workaround to avoid the play event by default -_-'
                m_Target.Stop();
            }


            bool playingState = greatestWeight == 1.0f;
            if (enabledStates[playableIndex] != playingState)
            {
                if (playingState)
                    m_Target.Play();
                else
                    m_Target.Stop();

                enabledStates[playableIndex] = playingState;
            }

            // blend to the default values
            //TODOPAUL: Clean
            //m_TrackBinding.color = Color.Lerp(m_DefaultColor, blendedColor, totalWeight);
            //m_TrackBinding.fontSize = Mathf.RoundToInt(Mathf.Lerp(m_DefaultFontSize, blendedFontSize, totalWeight));
            //m_TrackBinding.text = text;
        }

        public override void OnPlayableCreate(Playable playable)
        {
//see m_ScrubbingCacheHelper  in /CinemachineMixer.cs?L174:25
            //var test = (ScriptPlayable<VisualEffectControlPlayableBehaviour>)playable.GetInput(0);
            //var test2 = test.GetBehaviour();
            //var test3 = PlayableExtensions.GetDuration(playable.GetInput(0));

            enabledStates = new bool[playable.GetInputCount()];
            m_ScrubbingCacheHelper = null;
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            RestoreDefaults();
            enabledStates = null;
            m_ScrubbingCacheHelper = null;
        }

        void SetDefaults(VisualEffect vfx)
        {
            if (m_Target == vfx)
                return;

            RestoreDefaults();

            m_Target = vfx;
            if (m_Target != null)
            {
                //TODOPAUL: Clean
            }
        }

        void RestoreDefaults()
        {
            if (m_Target == null)
                return;

            //TODOPAUL: Clean
        }
    }
}
#endif
