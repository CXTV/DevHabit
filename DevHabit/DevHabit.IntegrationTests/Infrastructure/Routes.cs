namespace DevHabit.IntegrationTests.Infrastructure;

public static class Routes
{
    public static class Auth
    {
        public const string Register = "auth/register";
        public const string Login = "auth/login";
    }

    public static class Habits
    {
        public const string GetAll = "habits";
        public const string Create = "habits";
        public static string GetById(string id) => $"habits/{id}";
        public static string Update(string id) => $"habits/{id}";
        public static string Patch(string id) => $"habits/{id}";
        public static string Delete(string id) => $"habits/{id}";
    }

    public static class GitHub
    {
        public const string StoreAccessToken = "github/personal-access-token";
        public const string RevokeAccessToken = "github/personal-access-token";
        public const string GetProfile = "github/profile";
        public const string GetEvents = "github/events";
    }

}
