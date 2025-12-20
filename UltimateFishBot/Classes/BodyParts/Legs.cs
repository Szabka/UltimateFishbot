using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UltimateFishBot.Classes.Helpers;

namespace UltimateFishBot.Classes.BodyParts
{
    class Legs
    {
        public enum Path
        {
            JUMP = 0,
            FRONT_BACK = 1,
            LEFT_RIGHT = 2
        }

        public async Task DoMovement(Mouth mouth, CancellationToken cancellationToken)
        {
            switch ((Path)Properties.Settings.Default.AntiAfkMoves)
            {
                case Path.JUMP:
                    await MovePath(new Keys[] { Keys.Space }, cancellationToken);
                    await Task.Delay(500, cancellationToken);
                    break;
                case Path.FRONT_BACK:
                    await MovePath(new Keys[] { Keys.Up, Keys.Down }, cancellationToken);
                    break;
                case Path.LEFT_RIGHT:
                    await MovePath(new Keys[] { Keys.Left, Keys.Right }, cancellationToken);
                    break;
                default:
                    await MovePath(new Keys[] { Keys.Space }, cancellationToken);
                    await Task.Delay(500, cancellationToken);
                    break;
            }
            mouth?.Say("Anti A F K");
        }

        private async Task MovePath(Keys[] moves, CancellationToken cancellationToken)
        {
            foreach (Keys move in moves)
            {
                await SingleMove(move, cancellationToken);
                await Task.Delay(500, cancellationToken);
            }
        }

        private async Task SingleMove(Keys move, CancellationToken cancellationToken)
        {
            Win32.SendKeyboardAction(move, Win32.keyState.KEYDOWN);
            await Task.Delay(250, cancellationToken);
            Win32.SendKeyboardAction(move, Win32.keyState.KEYUP);
        }
    }
}
