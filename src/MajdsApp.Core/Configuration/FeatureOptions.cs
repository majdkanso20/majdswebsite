namespace MajdsApp.Configuration;

public class FeatureOptions
{
    public bool TwoFactorAuthenticationEnabled { get; set; } = true;
}

public class SmtpOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Majd's App";
}
