using Tap_Chat.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using com.shephertz.app42.paas.sdk.windows;
using com.shephertz.app42.paas.sdk.windows.session;
using System.Threading.Tasks;
using com.shephertz.app42.gaming.multiplayer.client;
using Tap_Chat.ViewModel;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace Tap_Chat
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class SignUpPage : Page
	{
		private NavigationHelper navigationHelper;
		private ObservableDictionary defaultViewModel = new ObservableDictionary();
		private bool inputEnabled = true;

		// Characters not allowed as inputs for username/password/email
		private char[] badChars = new char[] { ';', ' ', '\\', '/', '?', '!', '^', '%', '*', '$', '#', '@', '~' };

		// These store the encrypted versions of the entries on the page
		public string encryptedUsername, encryptedPassword, encryptedEmail;

		public SignUpPage()
		{
			this.InitializeComponent();

			this.navigationHelper = new NavigationHelper(this);
			this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
			this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

			AppServices.friendsData._page = this;
		}

		/// <summary>
		/// Gets the <see cref="NavigationHelper"/> associated with this <see cref="Page"/>.
		/// </summary>
		public NavigationHelper NavigationHelper
		{
			get { return this.navigationHelper; }
		}

		/// <summary>
		/// Gets the view model for this <see cref="Page"/>.
		/// This can be changed to a strongly typed view model.
		/// </summary>
		public ObservableDictionary DefaultViewModel
		{
			get { return this.defaultViewModel; }
		}

		/// <summary>
		/// Populates the page with content passed during navigation.  Any saved state is also
		/// provided when recreating a page from a prior session.
		/// </summary>
		/// <param name="sender">
		/// The source of the event; typically <see cref="NavigationHelper"/>
		/// </param>
		/// <param name="e">Event data that provides both the navigation parameter passed to
		/// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
		/// a dictionary of state preserved by this page during an earlier
		/// session.  The state will be null the first time a page is visited.</param>
		private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
		{
		}

		/// <summary>
		/// Preserves state associated with this page in case the application is suspended or the
		/// page is discarded from the navigation cache.  Values must conform to the serialization
		/// requirements of <see cref="SuspensionManager.SessionState"/>.
		/// </summary>
		/// <param name="sender">The source of the event; typically <see cref="NavigationHelper"/></param>
		/// <param name="e">Event data that provides an empty dictionary to be populated with
		/// serializable state.</param>
		private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
		{
		}

		// Show or hide the progress indicator depending on the value of isEnabled. If it is shown, show the message in indicatorMessage.
		public async void SetProgressIndicatorAsync(bool isEnabled, string indicatorMessage)
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				this.SetProgressIndicator(isEnabled, indicatorMessage);
			});
		}

		public void SetProgressIndicator(bool isEnabled, string indicatorMessage)
		{
			if (isEnabled)
			{
				progressText.Text = indicatorMessage;
				progressRingStack.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			else
			{
				progressText.Text = string.Empty;
				progressRingStack.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			}
		}

		private void StartAsyncOperations(string indicatorMessage)
		{
			SetProgressIndicator(true, indicatorMessage);
			if (this.inputEnabled)
				DisableInput();
			AppServices.operationSuccessful = false;
			AppServices.errorMessage = string.Empty;
		}

		private void EndAsyncOperationsError()
		{
			this.PrintMessage(AppServices.errorMessage);
			this.SetProgressIndicator(false, "");
			this.EnableInput();
			return;
		}

		private void EndAsyncOperationsSuccess()
		{
			this.SetProgressIndicator(false, "");
			this.EnableInput();
			return;
		}

		// Check the form for errors and then register a new user with the App42 cloud user base
		private async void signUpButton_Click(object sender, RoutedEventArgs e)
		{
			// Disable the user from making any changes and check for errors
			DisableInput();
			ClearErrorBoxes();

			string nameErrorMsg = CheckInput(usernameBox.Text);
			string pwdErrorMsg = CheckInput(passwordBox.Password);
			string chkPwdErrorMsg = "";

			if (confirmPasswordBox.Password.Equals(passwordBox.Password) == false)
			{
				chkPwdErrorMsg = "- Passwords do not match";
			}

			// Check for email entry errors (different checks than the others so it has no function)
			UInt32 atCount = 0;
			string emailErrorMsg = "";
			bool emailErrorOccured = false, firstBadChar = true;
			foreach (char c in emailBox.Text)
			{
				if (c.Equals('@'))
					atCount++;
				if (atCount > 1)
				{
					break;
				}
			}

			if (atCount != 1)
			{
				emailErrorOccured = true;
				emailErrorMsg = "- Incorrect format. Please use the format email@example.com";
			}

			foreach (char c in badChars)
			{
				if (emailBox.Text.Contains(c) && c != '@')
				{
					// If this is not the first error, first add a newline
					if (emailErrorOccured)
						emailErrorMsg += "\n";
					// Only add the error message part if this is the first bad character detected
					if (firstBadChar)
					{
						emailErrorMsg += "- Cannot contain character(s):";
						firstBadChar = false;
					}
					emailErrorMsg += " " + c.ToString();
					emailErrorOccured = true;
				}
			}

			if (string.IsNullOrWhiteSpace(emailBox.Text))
			{
				if (emailErrorOccured)
					emailErrorMsg += "\n";
				emailErrorMsg += "- Email cannot be empty or spaces.";
				emailErrorOccured = true;
			}

			// If there are errors, print the message and re-enable input. Otherwise register the user
			if (nameErrorMsg.Length > 0 || pwdErrorMsg.Length > 0 || chkPwdErrorMsg.Length > 0 || emailErrorOccured)
			{
				ShowErrorBoxes(nameErrorMsg, pwdErrorMsg, chkPwdErrorMsg, emailErrorMsg);
				PrintMessage("There are errors in the form. Please check your entries and try again.");
				EnableInput();
			}
			else
			{
				// Clear the error boxes, setup the progress bar, and create a user
				ClearErrorBoxes();
				SetProgressIndicator(true, "Encrypting your data");

				// Encrypt the input in a different thread
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
				{
					encryptedUsername = AppServices.EncryptString(usernameBox.Text, AppServices.secretKey);
					encryptedPassword = AppServices.EncryptString(passwordBox.Password, AppServices.secretKey);
					// Add @email.com to the encrypted email string so that it is recognized as valid
					encryptedEmail = AppServices.EncryptString(emailBox.Text, AppServices.secretKey) + "@email.com";
				});

				this.StartAsyncOperations("Connecting and registering...");
				AppServices.operationComplete = false;
				AppServices.userService.CreateUser(encryptedUsername, encryptedPassword, encryptedEmail, new SignUpCallBack());

				await Task.Run(() =>
					{
						while (AppServices.operationComplete == false) ;
					});

				if (AppServices.operationSuccessful)
				{
					this.Login();
					return;
				}
				else
				{
					this.EndAsyncOperationsError();
					return;
				}
			}
		}

		private async void Login()
		{
			// Since we are creating a new user no need to authenticate at this time
			AppServices.localUsername = usernameBox.Text;
			AppServices.localUsernameEncrypted = encryptedUsername;
			AppServices.app42Authenticated = true;

			if (AppServices.app42Authenticated)
			{
				AppServices.app42Authenticated = false;
			}

			if (AppServices.app42LoggedIn)
			{
				AppServices.app42LoggedIn = false;
				AppServices.sessionService.Invalidate(AppServices.localSessionId, new LogoutSessionCallback());
			}

			// Reset the operation status before starting the next operation
			this.StartAsyncOperations("Creating a session...");

			// Create a session for the local user
			if (AppServices.app42LoggedIn == false)
				AppServices.sessionService.GetSession(AppServices.localUsernameEncrypted, true, new LoginSessionCallback());

			// Wait for creating a session to complete
			await Task.Run(() =>
			{
				while (AppServices.app42LoggedIn == false) ;
			});

			if (AppServices.operationSuccessful == false)
			{
				AppServices.app42LoggedIn = false;
				this.EndAsyncOperationsError();
				return;
			}

			//If we have not initialized the AppWarp API yet, we do that now.
			if (AppServices.appWarpLoggedIn)
			{
				AppServices.appWarpLoggedIn = false;
				AppServices.warpClient.Disconnect();
			}
			else
			{
				this.StartAsyncOperations("Logging in...");
				WarpClient.initialize(AppServices.apiKey, AppServices.secretKey);
				WarpClient.setRecoveryAllowance(AppServices.appWarpRecoveryTime);
				AppServices.warpClient = WarpClient.GetInstance();

				AppServices.warpClient.AddConnectionRequestListener(AppServices.connListenObj);
				AppServices.warpClient.AddNotificationListener(AppServices.notificationListnerObj);
				AppServices.warpClient.AddUpdateRequestListener(AppServices.updateListenerObj);
			}
			AppServices.warpClient.Connect(AppServices.localUsername);

			await Task.Run(() =>
			{
				while (AppServices.appWarpLoggedIn == false) ;
			});

			// If the Connect failed, enable input and show an error message
			if (AppServices.operationSuccessful == false)
			{
				AppServices.appWarpLoggedIn = false;
				this.EndAsyncOperationsError();
				return;
			}

			// Since there are no friends or updates to send we just set these to true
			AppServices.app42LoadedFriends = true;
			AppServices.appWarpUpdateSent = true;

			if (AppServices.app42LoadedRequests)
			{
				AppServices.app42LoadedRequests = false;
				AppServices.friendsData.friendRequests.Clear();
			}
			this.StartAsyncOperations("Loading friend requests...");
			AppServices.friendsData.GetFriendRequestsFromCloud();

			await Task.Run(() =>
			{
				while (AppServices.app42LoadedRequests == false) ;
			});

			if (AppServices.operationSuccessful == false)
			{
				AppServices.app42LoadedRequests = false;
				this.EndAsyncOperationsError();
				return;
			}

			// Enabling this causes a small graphical glitch where the password box gets focus for a split second when transitioning
			//	to the next page so its better to just leave it off
			//this.EndAsyncOperationsSuccess();

			// If we reach this point then both authentication and creating a session were successful so we
			//	go to the next page
			this.Frame.Navigate(typeof(MainPage));
		}

		// Show the error messages if they exist
		private void ShowErrorBoxes(string usernameError, string pwdError, string chkPwdError, string emailError)
		{
			if (usernameError.Length > 0)
			{
				usernameErrors.Text = usernameError;
				usernameErrors.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			if (pwdError.Length > 0)
			{
				passwordErrors.Text = pwdError;
				passwordErrors.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			if (chkPwdError.Length > 0)
			{
				confirmPasswordErrors.Text = chkPwdError;
				confirmPasswordErrors.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			if (emailError.Length > 0)
			{
				emailErrors.Text = emailError;
				emailErrors.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
		}

		private void ClearErrorBoxes()
		{
			usernameErrors.Text = "";
			passwordErrors.Text = "";
			confirmPasswordErrors.Text = "";
			emailErrors.Text = "";

			usernameErrors.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			passwordErrors.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			confirmPasswordErrors.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			emailErrors.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
		}

		private string CheckInput(string inputText)
		{
			string errorMsg = "";
			bool errorOccured = false;		// Variable used to mark an error
			bool firstBadChar = true;

			// Usernames can only be 100 characters max
			if (inputText.Length > 100 || inputText.Length < 6)
			{
				errorMsg += "- Must be between 6 and 100 characters.";
				errorOccured = true;
			}
			// Usernames or passwords should not contain special characters
			foreach (char c in badChars)
			{
				if (inputText.Contains(c))
				{
					// If this is not the first error, first add a newline
					if (errorOccured)
						errorMsg += "\n";
					// Only add the error message part if this is the first bad character detected
					if (firstBadChar)
					{
						errorMsg += "- Cannot contain character(s):";
						firstBadChar = false;
					}
					errorMsg += " " + c.ToString();
					errorOccured = true;
				}
			}
			// Username cannot be empty or whitespace
			if (string.IsNullOrWhiteSpace(inputText))
			{
				// If this is not the first error, first add a newline
				if (errorOccured)
					errorMsg += "\n";

				errorMsg += "- Cannot be empty or spaces.";
				errorOccured = true;
			}

			return errorMsg;
		}

		public void EnableInput()
		{
			usernameBox.IsEnabled = true;
			passwordBox.IsEnabled = true;
			confirmPasswordBox.IsEnabled = true;
			emailBox.IsEnabled = true;

			this.inputEnabled = true;
		}

		public void DisableInput()
		{

			usernameBox.IsEnabled = false;
			passwordBox.IsEnabled = false;
			confirmPasswordBox.IsEnabled = false;
			emailBox.IsEnabled = false;

			this.inputEnabled = false;
		}

		private async void CreateUserComplete()
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				SetProgressIndicator(false, "");
				AppServices.localUsername = usernameBox.Text;
				AppServices.localUsernameEncrypted = encryptedUsername;
				this.Frame.Navigate(typeof(MainPage));
			});
		}

		public async void PrintMessage(string message)
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
			{
				// Exception handling added to stop crash when the user had an empty input. Not sure why this was happening but this fixes it.
				try
				{
					Windows.UI.Popups.MessageDialog msgDlg = new Windows.UI.Popups.MessageDialog(message);
					await msgDlg.ShowAsync();
				}
				catch (System.UnauthorizedAccessException)
				{
					return;
				}
			});
		}

		public class SignUpCallBack : App42Callback
		{
			public SignUpCallBack()
			{
			}
			public void OnException(App42Exception exception)
			{
				int appErrorCode = exception.GetAppErrorCode();
				int httpErrorCode = exception.GetHttpErrorCode();

				string errorMsg = "";

				switch (appErrorCode)
				{
					case 2001:
						errorMsg = "User Entry Error: Username already exists. Please try a different username.";
						break;
					case 2005:
						errorMsg = "User Entry Error: User with that email already exists. Please use a different email.";
						break;
					case 1400:
						errorMsg = "Network Error: The request parameters are invalid. Please check your network connection and try again.";
						break;
					case 1401:
						errorMsg = "Unauthorized Access Error: The client is not authorized to add a user.";
						break;
					case 1500:
						errorMsg = "Internal Server Error: Please try again.";
						break;
					default:
						errorMsg = "Unknown Error: An unknown error has occured. Please check your network connection and try again.";
						break;

				}

				AppServices.operationSuccessful = false;
				AppServices.errorMessage = errorMsg;
				AppServices.operationComplete = true;
			}

			public void OnSuccess(object response)
			{
				AppServices.operationSuccessful = true;
				AppServices.operationComplete = true;
			}
		}

		public class LoginSessionCallback : App42Callback
		{
			public LoginSessionCallback()
			{

			}

			public void OnException(App42Exception exception)
			{
				int appErrorCode = exception.GetAppErrorCode();
				int httpErrorCode = exception.GetHttpErrorCode();

				string errorMsg = string.Empty;

				switch (appErrorCode)
				{
					case 1400:
						errorMsg = "Network Error: The request parameters are invalid. Please check your network connection and try again.";
						break;
					case 1401:
						errorMsg = "Unauthorized Access Error: The client is not authorized to add a user.";
						break;
					case 1500:
						errorMsg = "Internal Server Error: Please try again.";
						break;
					case 2200:
						errorMsg = "Service Error: User by the given name does not exist.";
						break;
					case 2201:
						errorMsg = "Service Error: Session for user does not exist.";
						break;
					case 2202:
						errorMsg = "Service Error: Session with the given id does not exist.";
						break;
					case 2203:
						errorMsg = "Service Error: Bad request. The session for the given ID is already invalidated.";
						break;
					default:
						errorMsg = "Unknown Error: An unknown error has occured. Please check your network connection and try again.";
						break;
				}

				AppServices.errorMessage = errorMsg;
				AppServices.operationSuccessful = false;
				AppServices.app42LoggedIn = true;
			}

			// After a session is created successfully we store the session ID and move to the main page
			public void OnSuccess(object response)
			{
				// Save the session ID
				Session session = (Session)response;
				AppServices.localSessionId = session.sessionId;

				AppServices.operationSuccessful = true;
				AppServices.app42LoggedIn = true;
			}
		}

		#region NavigationHelper registration

		/// <summary>
		/// The methods provided in this section are simply used to allow
		/// NavigationHelper to respond to the page's navigation methods.
		/// <para>
		/// Page specific logic should be placed in event handlers for the  
		/// <see cref="NavigationHelper.LoadState"/>
		/// and <see cref="NavigationHelper.SaveState"/>.
		/// The navigation parameter is available in the LoadState method 
		/// in addition to page state preserved during an earlier session.
		/// </para>
		/// </summary>
		/// <param name="e">Provides data for navigation methods and event
		/// handlers that cannot cancel the navigation request.</param>
		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			this.navigationHelper.OnNavigatedTo(e);
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			this.navigationHelper.OnNavigatedFrom(e);
		}

		#endregion
	}
}
