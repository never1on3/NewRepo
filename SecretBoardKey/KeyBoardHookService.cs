using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SecretBoardKey
{
    public class KeyBoardHookService
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint LLKHF_INJECTED = 0x00000010;

        private const uint VK_PACKET = 0xE7; // код эмулированного Unicode-символа
        private static readonly IntPtr HOOK_MAGIC_NUMBER = new IntPtr(0x1337); // метка для SendInput

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private LowLevelKeyboardProc _hookDelegate;
        private IntPtr _hookID = IntPtr.Zero;

        public bool Start()
        {
            Stop(); // защита от двойного запуска

            _hookDelegate = HookCallback;
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _hookDelegate, GetModuleHandle(curModule.ModuleName), 0);
            }

            return _hookID != IntPtr.Zero;
        }

        public void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT pKeyStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                // КЛЮЧЕВАЯ ПРОВЕРКА НА ДУБЛИ
                bool isInjected = (pKeyStruct.flags & LLKHF_INJECTED) != 0;
                bool isPacket = pKeyStruct.vkCode == VK_PACKET;
                bool isOurSelfGeneratedInput = pKeyStruct.dwExtraInfo == HOOK_MAGIC_NUMBER;

                // если это эмулированный сигнал включая наш собственный пропускаем без обработки
                if (isInjected || isPacket || isOurSelfGeneratedInput)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                // проверяем раскладку
                IntPtr hwnd = GetForegroundWindow();
                uint threadId = GetWindowThreadProcessId(hwnd, out _);
                IntPtr currentLayout = GetKeyboardLayout(threadId);

                if ((ushort)((long)currentLayout & 0xFFFF) == 0x0419) // русская раскладка
                {
                    if (chipherAlphabet.VkTochipher.TryGetValue(pKeyStruct.vkCode, out string replacementStr))
                    {
                        // оотправляем замену с нашей меткой HOOK_MAGIC_NUMBER
                        CustomSendString(replacementStr);

                        // подавляем ориг нажатие физической клавиши
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void CustomSendString(string str)
        {
            List<INPUT> inputs = new List<INPUT>();

            foreach (char ch in str)
            {
                INPUT downInput = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wScan = ch,
                            dwFlags = KEYEVENTF_UNICODE,
                            dwExtraInfo = HOOK_MAGIC_NUMBER 
                        }
                    }
                };
                inputs.Add(downInput);

                INPUT upInput = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wScan = ch,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                            dwExtraInfo = HOOK_MAGIC_NUMBER // метка, чтобы хук узнал свое событие
                        }
                    }
                };
                inputs.Add(upInput);
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }
    }
    public class ExactDecrypter
    {
        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children = new();
            public char? TargetChar; // расшифрованный символ
        }

        private readonly TrieNode _root = new();

        public ExactDecrypter(Dictionary<string, uint> chipherAlphabetReverse)
        {
            foreach (var pair in chipherAlphabetReverse)
            {
                var node = _root;
                string cipherPattern = pair.Key;
                char originalChar = (char)pair.Value;

                foreach (char c in cipherPattern)
                {
                    if (!node.Children.TryGetValue(c, out var nextNode))
                    {
                        nextNode = new TrieNode();
                        node.Children[c] = nextNode;
                    }
                    node = nextNode;
                }
                node.TargetChar = originalChar;
            }
        }

        public string Decrypt(string input)
        {
            var result = new StringBuilder(input.Length);
            int i = 0;

            while (i < input.Length)
            {
                var currentNode = _root;
                int longestMatchLength = 0;
                char? matchedChar = null;

                for (int j = i; j < input.Length; j++)
                {
                    if (!currentNode.Children.TryGetValue(input[j], out currentNode))
                        break;

                    if (currentNode.TargetChar.HasValue)
                    {
                        matchedChar = currentNode.TargetChar.Value;
                        longestMatchLength = j - i + 1;
                    }
                }

                if (matchedChar.HasValue)
                {
                    result.Append(matchedChar.Value);
                    i += longestMatchLength;
                }
                else
                {
                    result.Append(input[i]);
                    i++;
                }
            }

            return result.ToString();
        }

    };

    public class UnicodeDecrypter
    {
        // лбъявляем приватное поле для хранения алфавита в классе
        private readonly Dictionary<uint, string> _alphabet;
        public UnicodeDecrypter(Dictionary<uint, string> alphabet)
        {
            _alphabet = alphabet ?? throw new ArgumentNullException(nameof(alphabet));
        }
        public string DecryptToUnicode(string inputText, Dictionary<uint, string> unicodeReserveAlphabet)
        {
            if (inputText == null) return string.Empty;
            if (unicodeReserveAlphabet == null)
                throw new ArgumentNullException(nameof(unicodeReserveAlphabet));

            var decryptedText = new StringBuilder(inputText.Length);

            foreach (char ch in inputText)
            {
                if (unicodeReserveAlphabet.TryGetValue((uint)ch, out string? replacement))
                    decryptedText.Append(replacement);
                else
                    decryptedText.Append(ch);
            }
            return decryptedText.ToString();
        }

        // перегрузка метода, использующая сохраненный в классе словарь _alphabet
        internal string DecryptToUnicode(string originalText)
        {
            return DecryptToUnicode(originalText, _alphabet);
        }

    }
}

