namespace YandexMusicPatcherGui
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            new System.Windows.Application().Run(new Main());
        }
        public const string ModPath = "YandexMusic";
        public const string Version = "2.0.7.4";
    }
}
