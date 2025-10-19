using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Diagnostics;
using System.IO;

public class LoginManager : MonoBehaviour
{
    [Header("Email Screen")]
    public GameObject emailScreen;
    public TMP_InputField emailField;
    public Button sendOTPButton;
    public TMP_Text emailErrorText;

    [Header("OTP Screen")]
    public GameObject otpScreen;
    public TMP_InputField otpField;
    public Button verifyOTPButton;
    public TMP_Text otpErrorText;

    [Header("Credit Score Selection Screen")]
    public GameObject creditScoreScreen;
    public Button badCreditButton;
    public Button averageCreditButton;
    public Button goodCreditButton;
    public TMP_Text creditScoreErrorText;

    // --- Configuration for Python Script ---
    private const string PythonPath = "python"; 
    private const string ScriptFileName = "Assets/Scripts/db_otp.py";
    private const string OTPFilePath = "Assets/otp.txt";
    
    // --- Credit Score Template Files ---
    private const string BadCreditTemplate = "bad_spending.json";
    private const string AverageCreditTemplate = "average_spending.json";
    private const string GoodCreditTemplate = "excellent_spending.json";
    private const string UserDataFolder = "UserData/";

    private string currentEmail;
    private string selectedCreditScore;

    void Start()
    {
        // Setup button listeners
        sendOTPButton.onClick.AddListener(HandleEmailSubmit);
        verifyOTPButton.onClick.AddListener(HandleOTPVerify);
        
        // Credit score button listeners
        badCreditButton.onClick.AddListener(() => HandleCreditScoreSelection("bad"));
        averageCreditButton.onClick.AddListener(() => HandleCreditScoreSelection("average"));
        goodCreditButton.onClick.AddListener(() => HandleCreditScoreSelection("good"));

        // Ensure UserData folder exists
        if (!Directory.Exists(UserDataFolder))
        {
            Directory.CreateDirectory(UserDataFolder);
        }

        // Show email screen first
        ShowEmailScreen();
    }

    void ShowEmailScreen()
    {
        emailScreen.SetActive(true);
        otpScreen.SetActive(false);
        creditScoreScreen.SetActive(false);
        emailErrorText.text = "";
        emailField.text = "";
    }

    void ShowOTPScreen()
    {
        emailScreen.SetActive(false);
        otpScreen.SetActive(true);
        creditScoreScreen.SetActive(false);
        otpErrorText.text = "";
        otpField.text = "";
    }

    void ShowCreditScoreScreen()
    {
        emailScreen.SetActive(false);
        otpScreen.SetActive(false);
        creditScoreScreen.SetActive(true);
        creditScoreErrorText.text = "";
    }

    void HandleEmailSubmit()
    {
        string email = emailField.text;

        // Validate email
        if (string.IsNullOrEmpty(email))
        {
            emailErrorText.text = "Email cannot be empty.";
            return;
        }
        if (!IsValidEmail(email))
        {
            emailErrorText.text = "Please enter a valid email address.";
            return;
        }

        // Execute Python script to generate and send OTP
        bool scriptSucceeded = RunPythonScript(email);

        if (scriptSucceeded)
        {
            currentEmail = email;
            emailErrorText.text = "";
            // Move to OTP screen
            ShowOTPScreen();
        }
        else
        {
            emailErrorText.text = "Failed to send OTP. Please try again.";
        }
    }

    void HandleOTPVerify()
    {
        string enteredOTP = otpField.text;

        // Validate OTP input
        if (string.IsNullOrEmpty(enteredOTP))
        {
            otpErrorText.text = "OTP cannot be empty.";
            return;
        }

        // Read the OTP from the file written by Python
        string correctOTP = ReadOTPFromFile();

        if (string.IsNullOrEmpty(correctOTP))
        {
            otpErrorText.text = "Error reading OTP. Please request a new one.";
            return;
        }

        // Compare OTPs
        if (enteredOTP.Trim() == correctOTP.Trim())
        {
            // OTP is correct, proceed to credit score selection
            otpErrorText.text = "";

            // Clean up the OTP file for security
            CleanupOTPFile();

            // Show credit score selection screen
            ShowCreditScoreScreen();
        }
        else
        {
            otpErrorText.text = "Invalid OTP. Please try again.";
        }
    }

    void HandleCreditScoreSelection(string creditScore)
    {
        selectedCreditScore = creditScore;

        // Create user-specific file based on credit score
        bool fileCreated = CreateUserFile(currentEmail, creditScore);

        if (fileCreated)
        {
            creditScoreErrorText.text = "";
            
            // Save the current user email to PlayerPrefs for later access
            PlayerPrefs.SetString("CurrentUserEmail", currentEmail);
            PlayerPrefs.SetString("CurrentUserCreditScore", creditScore);
            PlayerPrefs.Save();

            // Load the main game scene
            SceneManager.LoadScene("City");
        }
        else
        {
            creditScoreErrorText.text = "Error creating user profile. Please try again.";
        }
    }

    bool CreateUserFile(string email, string creditScore)
    {
        try
        {
            // Determine which template to use
            string templatePath = "";
            switch (creditScore)
            {
                case "bad":
                    templatePath = BadCreditTemplate;
                    break;
                case "average":
                    templatePath = AverageCreditTemplate;
                    break;
                case "good":
                    templatePath = GoodCreditTemplate;
                    break;
                default:
                    UnityEngine.Debug.LogError("Invalid credit score selection.");
                    return false;
            }

            // Check if template exists
            if (!File.Exists(templatePath))
            {
                UnityEngine.Debug.LogError($"Template file not found: {templatePath}");
                return false;
            }

            // Create unique filename based on email (sanitize email for filename)
            string sanitizedEmail = SanitizeEmailForFilename(email);
            string userFilePath = Path.Combine(UserDataFolder, $"{sanitizedEmail}_data.json");

            // Copy template to user file
            File.Copy(templatePath, userFilePath, overwrite: true);

            UnityEngine.Debug.Log($"User file created: {userFilePath}");
            return true;
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"Error creating user file: {ex.Message}");
            return false;
        }
    }

    string SanitizeEmailForFilename(string email)
    {
        // Replace invalid filename characters with underscores
        string sanitized = email.Replace("@", "_at_").Replace(".", "_");
        return sanitized;
    }

    bool RunPythonScript(string email)
    {
        string scriptPath = ScriptFileName;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = PythonPath,
            Arguments = $"-u \"{Path.GetFullPath(scriptPath)}\" \"{email}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Application.dataPath
        };

        try
        {
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    UnityEngine.Debug.LogError("Failed to start Python process.");
                    return false;
                }

                process.WaitForExit();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                int exitCode = process.ExitCode;

                UnityEngine.Debug.Log($"Python Output: {output}");

                if (exitCode == 0)
                {
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"Python Error (Exit Code {exitCode}): {error}");
                    return false;
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"Exception when running Python: {ex.Message}");
            return false;
        }
    }

    string ReadOTPFromFile()
    {
        try
        {
            if (File.Exists(OTPFilePath))
            {
                string otp = File.ReadAllText(OTPFilePath);
                return otp;
            }
            else
            {
                UnityEngine.Debug.LogError($"OTP file not found at: {OTPFilePath}");
                return null;
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"Error reading OTP file: {ex.Message}");
            return null;
        }
    }

    void CleanupOTPFile()
    {
        try
        {
            if (File.Exists(OTPFilePath))
            {
                File.Delete(OTPFilePath);
                UnityEngine.Debug.Log("OTP file cleaned up.");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"Error deleting OTP file: {ex.Message}");
        }
    }

    bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}