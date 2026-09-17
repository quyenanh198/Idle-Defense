namespace IdleHeroDefense.Presentation
{
    public enum AppScreen { Home, Heroes, Battle, Summon, Shop }

    public static class AppNavigation
    {
        public static AppScreen Current { get; private set; } = AppScreen.Home;
        public static void GoTo(AppScreen screen) => Current = screen;
    }
}

