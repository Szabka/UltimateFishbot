using System;
using System.Speech.Synthesis;
using UltimateFishBot.Properties;

namespace UltimateFishBot.Classes.BodyParts {
    class Mouth {
        private IProgress<string> m_progressHandle;
        private SpeechSynthesizer synthesizer;
        bool uset2s;
        string lastMessage;

        public Mouth(IProgress<string> progressHandle) {
            m_progressHandle = progressHandle;
            uset2s = Properties.Settings.Default.Txt2speech;
            if (uset2s) {
                synthesizer = new SpeechSynthesizer();
                synthesizer.Volume = 60;  // 0...100
                synthesizer.Rate = 1;   // -10...10
            }
        }

        public void Say(string text) {
            m_progressHandle.Report(text);
            if (uset2s && (lastMessage != text) && (synthesizer.State == SynthesizerState.Ready)) {
                // Say asynchronous text through Text 2 Speech synthesizer
                synthesizer.SpeakAsync(text);
                lastMessage = text;
            }
        }
    }
}
