using ScrapySharp.Extensions;
using ScrapySharp.Html.Forms;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;
using System.Windows.Forms;
using System.IO;
using HtmlAgilityPack;

namespace Lucky13
{
    public static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);



        private static string[] COMMON_WORDS = new string[] { "de", "het", "een", "van", "wie", "wat", "hoe", "new" };
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;


        public static void Main(string[] args)
        {
            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Console.WriteLine((Keys)vkCode);
                if ((Keys)vkCode == Keys.PrintScreen)
                {
                    Execute();
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
            }
        }


        public static void Execute ()
        {
            ScreenCapture.CaptureActiveWindow().Save("./capture.png");

            var testImagePath = "./capture.png";
            try
            {
                string question;
                string answer1;
                string answer2;

                using (var engine = new TesseractEngine(@"./tessdata", "nld", EngineMode.Default))
                {
                    using (var img = Pix.LoadFromFile(testImagePath))
                    {
                        using (var page = engine.Process(img, new Rect(img.Width / 20, img.Height / 8, img.Width - img.Width / 10, img.Height / 3)))
                        {
                            question = page.GetText().Replace("\n", " ").RemoveSpecialCharacters();
                        }

                        using (var page = engine.Process(img, new Rect(img.Width / 10, (img.Height / 2), img.Width - img.Width / 5, img.Height / 5 - 2)))
                        {
                            answer1 = page.GetText().Replace("\n", " ");
                        }

                        using (var page = engine.Process(img, new Rect(img.Width / 10, (img.Height / 2) + (img.Height / 5), img.Width - img.Width / 5, img.Height / 5 - 2)))
                        {
                            answer2 = page.GetText().Replace("\n", " ");
                        }
                    }
                }

                
                TestOne(question, answer1, answer2);
            }
            catch (Exception e)
            {
                Trace.TraceError(e.ToString());
                Console.WriteLine("Unexpected Error: " + e.Message);
                Console.WriteLine("Details: ");
                Console.WriteLine(e.ToString());
            }
        }

        public static void TestOne (string question, string answer1, string answer2)
        {
            ScrapingBrowser browser = new ScrapingBrowser();

            //set UseDefaultCookiesParser as false if a website returns invalid cookies format
            //browser.UseDefaultCookiesParser = false;


            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument google = web.Load("http://www.google.nl/search?q=" + question + "&num=100");
            HtmlAgilityPack.HtmlDocument bing = web.Load("http://www.bing.nl/search?q=" + question + "&cc=nl");

            string contentString = string.Concat(google.DocumentNode.InnerHtml, bing.DocumentNode.InnerHtml).ToLower().RemoveSpecialCharacters();

            string[] answers = new string[] { answer1.Replace("\n", "").RemoveCommonWords(), answer2.Replace("\n", "").RemoveCommonWords() };
            Dictionary<string, int> probabilities = new Dictionary<string, int>();

            foreach (string answer in answers)
            {
                if (answer == string.Empty) continue;
                probabilities.Add(answer, 0);

                string[] words = answer.Split(' ');
                List<string> foundWords = new List<string>();

                foreach (string word in words)
                {
                    if (word == string.Empty) continue;

                    int amountFound = Regex.Matches(contentString, word.RemoveSpecialCharacters().ToLower()).Count;
                    probabilities[answer] += amountFound;
                }
            }

            int totalCount = probabilities.Sum(x => x.Value);
            foreach (var pair in probabilities)
            {
                Console.WriteLine(pair.Key + " : " + ((probabilities[pair.Key] / (double)totalCount) * 100) + "%");
            }
        }

        public static string RemoveSpecialCharacters(this string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_' || c == ' ')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string RemoveCommonWords(this string str)
        {
            return string.Join(" ", str.ToLower().Split(' ').Except(COMMON_WORDS));
        }
    }
}
