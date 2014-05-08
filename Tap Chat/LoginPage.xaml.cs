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
using com.shephertz.app42.gaming.multiplayer.client;
using System.Threading.Tasks;
using Tap_Chat.ViewModel;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace Tap_Chat
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginPage : Page
    {
        private NavigationHelper navigationHelper;
		private bool inputEnabled = true;
        //private ObservableDictionary defaultViewModel = new ObservableDictionary();

        public LoginPage()
        {
            this.InitializeComponent();

            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
            this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

			AppServices.friendsData._page = this;

			// Create the default encryption key
			IBuffer keyBuffer = CryptographicBuffer.ConvertStringToBinary(AppServices.secretKey, BinaryStringEncoding.Utf8);
			AppServices.defaultEncryptionKey = AppServices.encryptionAlgorithim.CreateSymmetricKey(keyBuffer);
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
		/*
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }
		*/
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

		private void usernameTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			/***** Login to AppWarp for the authenticated user *****/
			/*
			//If we are already logged in with a different username we logout (this means the user changed the entered input)
			if (AppServices.appWarpLoggedIn && AppServices.localUsername.Equals(usernameTextBox.Text) == false)
			{
				AppServices.appWarpLoggedIn = false;
				AppServices.warpClient.Disconnect();
			}

			AppServices.localUsername = usernameTextBox.Text;
			AppServices.warpClient.Connect(usernameTextBox.Text);
			*/
		}

		private async void loginButton_Click(object sender, RoutedEventArgs e)
		{
			// Set the progress indicator, disable input, and authenticate the username and password entered by the user if it is not empty
			if (string.IsNullOrWhiteSpace(usernameTextBox.Text) == false && string.IsNullOrWhiteSpace(passwordBox.Password) == false)
			{
				/***** Authenticate the input *****/

				if (AppServices.app42Authenticated)
				{
					AppServices.app42Authenticated = false;
				}

				// Disable Input, Set the status message, and set the AsyncOperationSuccess variable to false
				this.StartAsyncOperations("Authenticating...");
				AppServices.localUsername = usernameTextBox.Text;
				AppServices.localUsernameEncrypted = AppServices.EncryptString(AppServices.localUsername, AppServices.secretKey);
				string encryptedPassword = AppServices.EncryptString(passwordBox.Password, AppServices.secretKey);
				AppServices.userService.Authenticate(AppServices.localUsernameEncrypted, encryptedPassword, new LoginCallback(this));

				// Wait for Authentication to complete
				await Task.Run(() =>
				{
					while (AppServices.app42Authenticated == false) ;
				});

				// If the Authentication failed. Enable input and show an error message
				if (AppServices.operationSuccessful == false)
				{
					AppServices.app42Authenticated = false;
					this.EndAsyncOperationsError();
					return;
				}

				// Temporary: Print the connection state and wait for a few seconds
				//AppServices.warpClient.GetConnectionState();

				/***** Create a session for the authenticated user *****/

				if (AppServices.app42LoggedIn)
				{
					AppServices.app42LoggedIn = false;
					AppServices.sessionService.Invalidate(AppServices.localSessionId, new LogoutSessionCallback());
				}
				
				// Reset the operation status before starting the next operation
				this.StartAsyncOperations("Creating a session...");

				// Create a session for the local user
				if (AppServices.app42LoggedIn == false)
					AppServices.sessionService.GetSession(AppServices.localUsernameEncrypted, true, new LoginSessionCallback(this));

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
				
				/***** Load App42 Friends for the authenticated user *****/

				// Load the friends list from the App42 Database, then get which friends are online, then send all the online friends an update
				//	telling them that you have signed on
				if (AppServices.app42LoadedFriends)
				{
					AppServices.app42LoadedFriends = false;
					AppServices.friendsData.friendsList.Clear();
				}
				
				this.StartAsyncOperations("Loading friends list...");
				AppServices.friendsData.GetFriendsFromCloud();

				await Task.Run(() =>
				{
					while (AppServices.app42LoadedFriends == false) ;
				});

				if (AppServices.operationSuccessful == false)
				{
					AppServices.app42LoadedFriends = false;
					this.EndAsyncOperationsError();
					return;
				}

				AppServices.app42LoadedFriends = false;
				this.StartAsyncOperations("Getting friends online statuses");
				if (AppServices.friendsData.friendsList.Count > 0)
				{
					foreach (FriendsListEntry friend in AppServices.friendsData.friendsList)
					{
						AppServices.sessionService.GetSession(friend.encryptedUsername, false, new GetFriendSessionsCallback(friend.username));
					}

					await Task.Run(() =>
					{
						while (AppServices.app42LoadedFriends == false) ;
					});

					if (AppServices.operationSuccessful == false)
					{
						AppServices.app42LoadedFriends = false;
						this.EndAsyncOperationsError();
						return;
					}
				}
				else
				{
					AppServices.app42LoadedFriends = true;
				}


				/***** Load friend requests for the user *****/

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

				// If we reach this point then both authentication and creating a session were successful so we
				//	go to the next page
				this.Frame.Navigate(typeof(MainPage));
			}
			else
			{
				PrintMessage("ERROR: Username and/or Password cannnot be empty. Please try again.");
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

		public void DisableInput()
		{
			usernameTextBox.IsEnabled = false;
			passwordBox.IsEnabled = false;
			loginButton.IsEnabled = false;
			signUpButton.IsEnabled = false;

			this.inputEnabled = false;
		}

		public async void DisableInputAsync()
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				DisableInput();
			});
		}

		public void EnableInput()
		{
			usernameTextBox.IsEnabled = true;
			passwordBox.IsEnabled = true;
			loginButton.IsEnabled = true;
			signUpButton.IsEnabled = true;

			this.inputEnabled = true;
		}

		public async void EnableInputAsync()
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				this.EnableInput();
			});
		}

		public async void PrintMessage(string message)
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
		}

		public async void PrintMessageAsync(string message)
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				PrintMessage(message);
			});
		}

		// Load the main page and save data
		public void LoginSuccessful()
		{
			this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				SetProgressIndicator(false, "");
				this.Frame.Navigate(typeof(MainPage));
			});
		}

		private void signUpButton_Click(object sender, RoutedEventArgs e)
		{
			this.Frame.Navigate(typeof(SignUpPage));
		}

		public class LoginCallback : App42Callback
		{
			private LoginPage _page;

			public LoginCallback(LoginPage currPage)
			{
				_page = currPage;
			}

			public void OnException(App42Exception exception)
			{
				AppServices.errorMessage = "Incorrect usesrname or password. Please try again.";
				AppServices.app42Authenticated = true;
				AppServices.operationSuccessful = false;
			}

			public void OnSuccess(object response)
			{
				AppServices.app42Authenticated = true;
				AppServices.operationSuccessful = true;
			}
		}

		public class LoginSessionCallback : App42Callback
		{
			private LoginPage _page;
			
			public LoginSessionCallback()
			{

			}

			public LoginSessionCallback(LoginPage currPage)
			{
				_page = currPage;
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
