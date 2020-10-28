using DevionGames.LoginSystem.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using Firebase.Extensions;
using UnityEngine.SceneManagement;


namespace DevionGames.LoginSystem
{
    public class LoginManager : MonoBehaviour
    {
		private static LoginManager m_Current;
		
		protected static Firebase.Auth.FirebaseAuth auth;
        protected Dictionary<string, Firebase.Auth.FirebaseUser> userByAuth =
          new Dictionary<string, Firebase.Auth.FirebaseUser>();	

		private bool fetchingToken = false;	
		
		Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;

		/// <summary>
		/// The LoginManager singleton object. This object is set inside Awake()
		/// </summary>
		public static LoginManager current
		{
			get
			{
				Assert.IsNotNull(m_Current, "Requires Login Manager.Create one from Tools > Devion Games > Login System > Create Login Manager!");
				return m_Current;
			}
		}

		/// <summary>
		/// Awake is called when the script instance is being loaded.
		/// </summary>
		private void Awake()
		{
			if (LoginManager.m_Current != null)
			{
				if (LoginManager.DefaultSettings.debug)
					Debug.Log("Multiple LoginManager in scene...this is not supported. Destroying instance!");
				Destroy(gameObject);
				return;
			}
			else
			{
				LoginManager.m_Current = this;
				if(LoginManager.DefaultSettings.debug)
					Debug.Log("LoginManager initialized.");

			}
		}

        private void Start()
        {
			Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
				dependencyStatus = task.Result;
				if (dependencyStatus == Firebase.DependencyStatus.Available)
				{
						InitializeFirebase();
				}
				else
				{
					Debug.LogError(
						"Could not resolve all Firebase dependencies: " + dependencyStatus);
				}
			});	
			if (LoginManager.DefaultSettings.skipLogin)
			{
				if (LoginManager.DefaultSettings.debug)
					Debug.Log("Login System is disabled...Loading "+ LoginManager.DefaultSettings.sceneToLoad+" scene.");
				UnityEngine.SceneManagement.SceneManager.LoadScene(LoginManager.DefaultSettings.sceneToLoad);
			}

		}


        [SerializeField]
		private LoginConfigurations m_Configurations = null;

		/// <summary>
		/// Gets the login configurations. Configurate it inside the editor.
		/// </summary>
		/// <value>The database.</value>
		public static LoginConfigurations Configurations
		{
			get
			{
				if (LoginManager.current != null)
				{
					Assert.IsNotNull(LoginManager.current.m_Configurations, "Please assign Login Configurations to the Login Manager!");
					return LoginManager.current.m_Configurations;
				}
				return null;
			}
		}


		private static Default m_DefaultSettings;
		public static Default DefaultSettings
		{
			get
			{
				if (m_DefaultSettings == null)
				{
					m_DefaultSettings = GetSetting<Default>();
				}
				return m_DefaultSettings;
			}
		}

		private static UI m_UI;
		public static UI UI
		{
			get
			{
				if (m_UI == null)
				{
					m_UI = GetSetting<UI>();
				}
				return m_UI;
			}
		}

		private static Notifications m_Notifications;
		public static Notifications Notifications
		{
			get
			{
				if (m_Notifications == null)
				{
					m_Notifications = GetSetting<Notifications>();
				}
				return m_Notifications;
			}
		}

		private static T GetSetting<T>() where T : Configuration.Settings
		{
			if (LoginManager.Configurations != null)
			{
				return (T)LoginManager.Configurations.settings.Where(x => x.GetType() == typeof(T)).FirstOrDefault();
			}
			return default(T);
		}


        protected void InitializeFirebase()
        {
            Debug.Log("Setting up Firebase Auth");
            auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            auth.StateChanged += AuthStateChanged;
            auth.IdTokenChanged += IdTokenChanged;
            // Specify valid options to construct a secondary authentication object.
            AuthStateChanged(this, null);
        }


        void AuthStateChanged(object sender, System.EventArgs eventArgs)
        {
            Firebase.Auth.FirebaseAuth senderAuth = sender as Firebase.Auth.FirebaseAuth;
            Firebase.Auth.FirebaseUser user = null;
            if (senderAuth != null) userByAuth.TryGetValue(senderAuth.App.Name, out user);
            if (senderAuth == auth && senderAuth.CurrentUser != user)
            {
                bool signedIn = user != senderAuth.CurrentUser && senderAuth.CurrentUser != null;
                if (!signedIn && user != null)
                {
                    Debug.Log("Signed out " + user.UserId);
                }
                user = senderAuth.CurrentUser;
                userByAuth[senderAuth.App.Name] = user;
                if (signedIn)
                {
                    Debug.Log("AuthStateChanged Signed in " + user.UserId);
                }
            }
        }

		void IdTokenChanged(object sender, System.EventArgs eventArgs)
        {
            Firebase.Auth.FirebaseAuth senderAuth = sender as Firebase.Auth.FirebaseAuth;
            if (senderAuth == auth && senderAuth.CurrentUser != null && !fetchingToken)
            {
                senderAuth.CurrentUser.TokenAsync(false).ContinueWithOnMainThread(
                  task => Debug.Log(String.Format("Token[0:8] = {0}", task.Result.Substring(0, 8))));
            }
        }


		// Fetch and display current user's auth token.
        public void GetUserToken()
        {
            if (auth.CurrentUser == null)
            {
                Debug.Log("Not signed in, unable to get token.");
                return;
            }
            Debug.Log("Fetching user token");
            fetchingToken = true;
            auth.CurrentUser.TokenAsync(false).ContinueWithOnMainThread(task => {
                fetchingToken = false;
                if (LogTaskCompletion(task, "User token fetch"))
                {
                    Debug.Log("Token = " + task.Result);
                }
            });
        }

 		protected static bool LogTaskCompletion(Task task, string operation)
        {
            bool complete = false;
            if (task.IsCanceled)
            {
                Debug.Log(operation + " canceled.");
            }
            else if (task.IsFaulted)
            {
                Debug.Log(operation + " encounted an error.");
                foreach (Exception exception in task.Exception.Flatten().InnerExceptions)
                {
                    string authErrorCode = "";
                    Firebase.FirebaseException firebaseEx = exception as Firebase.FirebaseException;
                    if (firebaseEx != null)
                    {
                        authErrorCode = String.Format("AuthError.{0}: ",
                          ((Firebase.Auth.AuthError)firebaseEx.ErrorCode).ToString());
                    }
                    Debug.Log(authErrorCode + exception.ToString());
                }
            }
            else if (task.IsCompleted)
            {
                Debug.Log(operation + " completed");
                complete = true;
            }
            return complete;
        }

		public static void CreateAccount(string username, string password, string email)
		{
			if (LoginManager.current != null)
			{
				LoginManager.current.StartCoroutine(CreateAccountInternal(username, password, email));
			}
		}

		private static IEnumerator CreateAccountInternal(string username, string password, string email)
		{
			if (LoginManager.Configurations == null)
			{
				EventHandler.Execute("OnFailedToCreateAccount");
				yield break;
			}
			if (LoginManager.DefaultSettings.debug)
				Debug.Log("[CreateAccount]: Trying to register a new account with username: " + username + " and password: " + password + "!");


			Debug.Log(String.Format("Attempting to create user {0}...", email));

			auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
				if (task.IsCanceled) {
					Debug.LogError("CreateUserWithEmailAndPasswordAsync was canceled.");
					EventHandler.Execute("OnFailedToCreateAccount");
					return;
				}
				if (task.IsFaulted) {
					Debug.LogError("CreateUserWithEmailAndPasswordAsync encountered an error: " + task.Exception);
					EventHandler.Execute("OnFailedToCreateAccount");
					return;
				}
				EventHandler.Execute("OnAccountCreated");
				// Firebase user has been created.
				Firebase.Auth.FirebaseUser newUser = task.Result;
				Debug.LogFormat("Firebase user created successfully: {0} ({1})",
					newUser.DisplayName, newUser.UserId);
			});
		}

		/// <summary>
		/// Logins the account.
		/// </summary>
		/// <param name="username">Username.</param>
		/// <param name="password">Password.</param>
		public static void LoginAccount(string username, string password)
		{
			if (LoginManager.current != null)
			{
				LoginManager.current.StartCoroutine(LoginAccountInternal(username, password));
			}
		}

		private static IEnumerator LoginAccountInternal(string email, string password)
		{
			if (LoginManager.Configurations == null)
			{
				//Debug.LogError("SignInWithEmailAndPasswordAsync was canceled.");
				EventHandler.Execute("OnFailedToLogin");
				yield break;
			}
			if (LoginManager.DefaultSettings.debug)
				Debug.Log("[LoginAccount] Trying to login using username: " + email + " and password: " + password + "!");

			bool isauthenticated = false;
			yield return new YieldTask(
			auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
				if (task.IsCanceled) {
					Debug.LogError("SignInWithEmailAndPasswordAsync was canceled.");
					//EventHandler.Execute("OnFailedToLogin");
					return;
				}
				if (task.IsFaulted) {
					Debug.LogError("SignInWithEmailAndPasswordAsync encountered an error: " + task.Exception);
					//EventHandler.Execute("OnFailedToLogin");
					return;
				}
 				if (LogTaskCompletion(task, "Sign-in")) {
					isauthenticated = true;
					Debug.LogFormat("User isauthenticated: {0} ",
						isauthenticated );
					Firebase.Auth.FirebaseUser newUser = task.Result;
					UnityEngine.SceneManagement.SceneManager.LoadScene(2);

					Debug.LogFormat("User signed in successfully: {0} ({1})",
						newUser.DisplayName, newUser.UserId);
					return;
      			}
			}));			
			if(isauthenticated) {
				Debug.Log("Load Scene 2");
				GameManager.instance.account = email;
				UnityEngine.SceneManagement.SceneManager.LoadScene(2);
			}
		}

		/// <summary>
		/// Recovers the password.
		/// </summary>
		/// <param name="email">Email.</param>
		public static void RecoverPassword(string email)
		{
			if (LoginManager.current != null)
			{
				LoginManager.current.StartCoroutine(RecoverPasswordInternal(email));
			}
		}

		private static IEnumerator RecoverPasswordInternal(string emailAddress)
		{
			if (LoginManager.Configurations == null)
			{
				EventHandler.Execute("OnFailedToRecoverPassword");
				yield break;
			}
			if (LoginManager.DefaultSettings.debug)
				Debug.Log("[RecoverPassword] Trying to recover password using email: " + emailAddress + "!");


			auth.SendPasswordResetEmailAsync(emailAddress).ContinueWith(task => {
				if (task.IsCanceled) {
					Debug.LogError("SendPasswordResetEmailAsync was canceled.");
					EventHandler.Execute("OnFailedToRecoverPassword");
					return;
				}
				if (task.IsFaulted) {
					Debug.LogError("SendPasswordResetEmailAsync encountered an error: " + task.Exception);
					EventHandler.Execute("OnFailedToRecoverPassword");
					return;
				}
				EventHandler.Execute("OnPasswordRecovered");
				Debug.Log("Password reset email sent successfully.");
			});
		}

		/// <summary>
		/// Resets the password.
		/// </summary>
		/// <param name="username">Username.</param>
		/// <param name="password">Password.</param>
		public static void ResetPassword(string username, string password)
		{
			if (LoginManager.current != null)
			{
				LoginManager.current.StartCoroutine(ResetPasswordInternal(username, password));
			}
		}

		private static IEnumerator ResetPasswordInternal(string username, string password)
		{
			if (LoginManager.Configurations == null)
			{
				EventHandler.Execute("OnFailedToResetPassword");
				yield break;
			}
			if (LoginManager.DefaultSettings.debug)
				Debug.Log("[ResetPassword] Trying to reset password using username: " + username + " and password: " + password + "!");


			Firebase.Auth.FirebaseUser user = auth.CurrentUser;
			string newPassword = password;
			if (user != null) {
				user.UpdatePasswordAsync(newPassword).ContinueWith(task => {
					if (task.IsCanceled) {
						Debug.LogError("UpdatePasswordAsync was canceled.");
						EventHandler.Execute("OnFailedToResetPassword");
						return;
					}
					if (task.IsFaulted) {
						Debug.LogError("UpdatePasswordAsync encountered an error: " + task.Exception);
						EventHandler.Execute("OnFailedToResetPassword");
						return;
					}
					EventHandler.Execute("OnPasswordResetted");
					Debug.Log("Password updated successfully.");
				});
			}
		}

		/// <summary>
		/// Validates the email.
		/// </summary>
		/// <returns><c>true</c>, if email was validated, <c>false</c> otherwise.</returns>
		/// <param name="email">Email.</param>
		public static bool ValidateEmail(string email)
		{
			System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$");
			System.Text.RegularExpressions.Match match = regex.Match(email);
			if (match.Success)
			{
				if (LoginManager.DefaultSettings.debug)
					Debug.Log("Email validation was successfull for email: " + email + "!");
			}
			else
			{
				if (LoginManager.DefaultSettings.debug)
					Debug.Log("Email validation failed for email: " + email + "!");
			}

			return match.Success;
		}
	}
}