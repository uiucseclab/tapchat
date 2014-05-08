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
using Tap_Chat.ViewModel;
using com.shephertz.app42.paas.sdk.windows;
using com.shephertz.app42.paas.sdk.windows.buddy;
using com.shephertz.app42.gaming.multiplayer.client;
using System.Threading.Tasks;
using com.shephertz.app42.paas.sdk.windows.session;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace Tap_Chat
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		private NavigationHelper navigationHelper;
		//private ObservableDictionary defaultViewModel = new ObservableDictionary();
		private FriendsListEntry lastSelected = null;
		private FriendRequest lastSelectedRequest = null;
		private bool inputEnabled = true;

		private long publishedMessageID = -1;
		private long subscribedMessageID = -1;
		private IBuffer keyBuff = null;
		private string startNFCMessageType = "Windows.TapChatEstablishSession";

		public MainPage()
		{
			this.InitializeComponent();

			this.navigationHelper = new NavigationHelper(this);
			this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
			this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

			this.NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;

			// Set the data context to the friendsData global variable
			AppServices.friendsData._page = this;
			AppServices.friendsData.myUsername = AppServices.localUsername;
			DataContext = AppServices.friendsData;
			AppServices.mainPage = this;

			WarpClient.initialize(AppServices.apiKey, AppServices.secretKey);
			WarpClient.setRecoveryAllowance(AppServices.appWarpRecoveryTime);
			AppServices.warpClient = WarpClient.GetInstance();

			AppServices.warpClient.AddConnectionRequestListener(AppServices.connListenObj);
			AppServices.warpClient.AddNotificationListener(AppServices.notificationListnerObj);
			AppServices.warpClient.AddUpdateRequestListener(AppServices.updateListenerObj);

			// Connect to Appwarp and update our online friends in the callback
			AppServices.appWarpConnectMode = appWarpConnectModes.sendAllUpdates;
			WarpClient.GetInstance().Connect(AppServices.localUsername);	
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
			// Clear the back stack after logging in
			this.Frame.BackStack.Clear();
			this.DisplayFriendsList();
			this.DisplayFriendRequestsList();

			//AppServices.warpClient.GetConnectionState();

			/*if (AppServices.appWarpUpdateSent == false)
			{
				this.SendStatusUpdatesToFriends(true);
			}*/

			// Anytime we arrive back at the main page clear the current friend's name who we are chatting with in case this is a return from the chatPage
			AppServices.friendsData.currFriendsName = string.Empty;
			
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

		public void SendStatusUpdatesToFriends(bool myOnlineStatus)
		{
			foreach (FriendsListEntry friend in AppServices.friendsData.friendsList)
			{
				if (friend.isOnline)
				{
					AppServices.appWarpLastSentToUsername = friend.username;
					if (friend.sessionEstablished)
						AppServices.warpClient.sendPrivateUpdate(friend.username, MessageBuilder.buildOnlineStatusUpdateBytes(true, friend.encryptionKey));
					else
						AppServices.warpClient.sendPrivateUpdate(friend.username, MessageBuilder.buildOnlineStatusUpdateBytes(true, AppServices.defaultEncryptionKey));

				}
			}
		}

		public async void GetFriendsOnlineStatusAsync()
		{
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
					this.PrintMessage(AppServices.errorMessage);
				}
			}
		}

		public void StartAsyncOperations(string indicatorMessage)
		{
			SetProgressIndicator(true, indicatorMessage);
			if (this.inputEnabled)
				DisableInput();
			AppServices.operationSuccessful = false;
			AppServices.errorMessage = string.Empty;
		}

		public void EndAsyncOperationsError()
		{
			this.PrintMessage(AppServices.errorMessage);
			this.SetProgressIndicator(false, "");
			this.EnableInput();
			return;
		}

		public void EndAsyncOperationsSuccess()
		{
			this.SetProgressIndicator(false, "");
			this.EnableInput();
			return;
		}
/*
		// Gets the friends data from the cloud and shows it on screen
		public void LoadFriendsList()
		{
			// No data initialized, so return
			if (AppServices.friendsData == null)
			{
				return;
			}
			// We have not yet gotten the friends from the cloud so we do that now
			else if (AppServices.friendsData.friendsList == null || AppServices.loadedFriends == false)
			{
				AppServices.friendsData.GetFriendsFromCloud();
			}
			// Friends data has already been loaded so just display it
			else
			{
				this.DisplayFriendsList();
			}
		}

		public void LoadFriendsListAsync()
		{
			this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				LoadFriendsList();
			});
		}
*/
		// Toggle's the XAML visibilities to show or hide the friends lists
		public void DisplayFriendsList()
		{
			// If there are no friends, show a message about adding friends. (<= check just for robustness, the count should always be > 0)
			if (AppServices.friendsData.friendsList == null || AppServices.friendsData.friendsList.Count <= 0)
			{
				friendsListView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				emptyMsgBox.Text = "You have no friends yet :(\n\nTry adding some by pressing + below!";
				emptyMsgBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			// Otherwise show the friends list if it is not already shown
			else if (friendsListView.Visibility == Windows.UI.Xaml.Visibility.Collapsed)
			{
				friendsListView.Visibility = Windows.UI.Xaml.Visibility.Visible;
				emptyMsgBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			}
		}

		public async void DisplayFriendsListAsync()
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				DisplayFriendsList();
			});
		}

		public void UpdateOnlineStatus(string friendUsername, bool newOnlineStatus)
		{
			//PrintMessage("UpdateOnlineStatus Function");
			//return;
			if(AppServices.friendsData != null && AppServices.friendsData.friendsList != null)
			{
				AppServices.friendsData.UpdateOnlineStatus(friendUsername, newOnlineStatus);
			}
		}

		public async void UpdateOnlineStatusAsync(string friendUsername, bool newOnlineStatus)
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				this.UpdateOnlineStatus(friendUsername, newOnlineStatus);
			});
		}

		public void LoadFriendRequestList()
		{
			PrintMessage("LoadFriendRequestList Function");
			return;
			// No data initialized, so return
			if (AppServices.friendsData == null)
			{
				return;
			}
			// We have not yet gotten the friend requests from the cloud so we do that now
			else if (AppServices.friendsData.friendRequests == null)
			{
				//DisableInput();
				//SetProgressIndicator(true, "Getting friend requests from the cloud");
				AppServices.friendsData.GetFriendRequestsFromCloud();
			}
			// Friends data has already been loaded so just display it
			else
			{
				this.DisplayFriendRequestsList();
			}
		}

		public void LoadFriendRequestListAsync()
		{
			this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				LoadFriendRequestList();
			});
		}

		public void DisplayFriendRequestsList()
		{
			// If there are no friend requests, show a message
			if (AppServices.friendsData.friendRequests == null || AppServices.friendsData.friendRequests.Count == 0)
			{
				requestsListView.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				noRequestsMsgBox.Text = "You have no pending friend requests.";
				noRequestsMsgBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			// Otherwise show the friends list
			else
			{
				requestsListView.Visibility = Windows.UI.Xaml.Visibility.Visible;
				noRequestsMsgBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			}
		}

		public void DisplayFriendRequestsListAsync()
		{
			this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				this.DisplayFriendRequestsList();
			});
		}

		// Show or hide the progress indicator depending on the value of isEnabled. If it is shown, show the message in indicatorMessage.
		private void SetProgressIndicator(bool isEnabled, string indicatorMessage)
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

		public async void SetProgressIndicatorAsync(bool isEnabled, string indicatorMessage)
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				SetProgressIndicator(isEnabled, indicatorMessage);
			});
		}

		private void DisableInput()
		{
			addFriendAppBar.IsEnabled = false;
			editFriendsAppBar.IsEnabled = false;
			friendsListView.IsEnabled = false;
			friendsListView.Opacity = 0.2;

			requestsListView.IsEnabled = false;
			requestsListView.Opacity = 0.2;

			this.inputEnabled = false;
		}

		private void EnableInput()
		{
			addFriendAppBar.IsEnabled = true;
			editFriendsAppBar.IsEnabled = true;
			friendsListView.IsEnabled = true;
			friendsListView.Opacity = 1.0;

			requestsListView.IsEnabled = true;
			requestsListView.Opacity = 1.0;

			this.inputEnabled = true;
		}

		public async void DisableInputAsync()
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				DisableInput();
			});
		}

		public async void EnableInputAsync()
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				EnableInput();
			});
		}

		private void nfcChatButton_Click(object sender, RoutedEventArgs e)
		{
			if (lastSelected.isOnline == false)
			{
				this.PrintMessage("User is not online, cannot start chat.");
				return;
			}
			if (lastSelected.sessionEstablished)
			{
				lastSelected.chatButtonsVisible = false;
				lastSelected = null;
				this.Frame.Navigate(typeof(ChatPage));
				return;
			}
			if (AppServices.myProximityDevice == null)
			{
				AppServices.myProximityDevice = Windows.Networking.Proximity.ProximityDevice.GetDefault();
				if (AppServices.myProximityDevice == null)
				{
					this.PrintMessage("Unable to get NFC chip. This device might not be capable of proximity interactions or NFC is disabled in settings");
					return;
				}
				AppServices.myProximityDevice.DeviceArrived += ProximityDeviceArrived;
				AppServices.myProximityDevice.DeviceDeparted += ProximityDeviceDeparted;
			}
			int compareResult = string.Compare(AppServices.localUsernameEncrypted, lastSelected.encryptedUsername);
			this.keyBuff = null;
			// My username is greater so I generate a key
			if (compareResult > 0)
			{
				keyBuff = CryptographicBuffer.GenerateRandom(256);
			}

			PrintMessage("Please close this message and then tap and hold the devices together");
		}

		private void ProximityDeviceArrived(object sender)
		{
			// I did not generate a key so I receive the other key. I only need to subscribe
			if (this.keyBuff == null)
			{
				this.subscribedMessageID = AppServices.myProximityDevice.SubscribeForMessage(this.startNFCMessageType, ReceiveEncryptionKey);
			}
			// I generated a key so I publish and establish a session
			else
			{
				this.publishedMessageID = AppServices.myProximityDevice.PublishMessage(this.startNFCMessageType, CryptographicBuffer.EncodeToHexString(keyBuff));
				lastSelected.encryptionKey = AppServices.encryptionAlgorithim.CreateSymmetricKey(this.keyBuff);
				this.PrintMessageAsync("Secure connection established! You may now separate your devices.");
			}
		}

		private void ReceiveEncryptionKey(Windows.Networking.Proximity.ProximityDevice sender, Windows.Networking.Proximity.ProximityMessage message)
		{
			string keyString = message.DataAsString;
			this.keyBuff = CryptographicBuffer.DecodeFromHexString(keyString);
			lastSelected.encryptionKey = AppServices.encryptionAlgorithim.CreateSymmetricKey(this.keyBuff);
			this.PrintMessageAsync("Secure connection established! You may now separate your devices.");
		}

		private void ProximityDeviceDeparted(object sender)
		{
			if (this.publishedMessageID != -1)
			{
				AppServices.myProximityDevice.StopPublishingMessage(this.publishedMessageID);
				this.publishedMessageID = -1;
			}
			if (this.subscribedMessageID != -1)
			{
				AppServices.myProximityDevice.StopSubscribingForMessage(this.subscribedMessageID);
				this.subscribedMessageID = -1;
			}

			AppServices.myProximityDevice.DeviceArrived -= ProximityDeviceArrived;
			AppServices.myProximityDevice.DeviceDeparted -= ProximityDeviceDeparted;

			this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				// Set the binding variables to the data from the current friend we are chatting with and hide the chat buttons
				AppServices.friendsData.currFriendsName = lastSelected.username;
				AppServices.friendsData.chatHistory = lastSelected.history;
				AppServices.friendsData.sendButtonEnabled = true;
				lastSelected.chatButtonsVisible = false;
				lastSelected = null;
				this.Frame.Navigate(typeof(ChatPage));
			});
		}

		// Compute the encryption keys and load the chat page
		private void webChatButton_Click(object sender, RoutedEventArgs e)
		{
			// Tell the user we selected that a session is being established
			if (lastSelected.isOnline && string.IsNullOrWhiteSpace(lastSelected.sessionID) == false)
			{
				AppServices.appWarpLastSentToUsername = lastSelected.username;
				AppServices.warpClient.sendPrivateUpdate(lastSelected.username, MessageBuilder.bulidSessionEstablishedBytes(AppServices.defaultEncryptionKey));
			}
			else if (lastSelected.isOnline == false)
			{
				return;
			}

			

			// No session exists so we create a session
			int compareResult = string.Compare(AppServices.localUsernameEncrypted, lastSelected.encryptedUsername);
			this.keyBuff = null;
			// My username is greater so we use my sessionID as the encryptionKey
			if (compareResult > 0)
			{
				keyBuff = CryptographicBuffer.ConvertStringToBinary(AppServices.localSessionId, BinaryStringEncoding.Utf8);
			}
			else
			{
				keyBuff = CryptographicBuffer.ConvertStringToBinary(lastSelected.sessionID, BinaryStringEncoding.Utf8);
			}
			lastSelected.encryptionKey = AppServices.encryptionAlgorithim.CreateSymmetricKey(keyBuff);
			// Set the binding variables to the data from the current friend we are chatting with and hide the chat buttons
			AppServices.friendsData.currFriendsName = lastSelected.username;
			AppServices.friendsData.chatHistory = lastSelected.history;
			AppServices.friendsData.sendButtonEnabled = true;
			lastSelected.chatButtonsVisible = false;
			lastSelected = null;
			this.Frame.Navigate(typeof(ChatPage));
		}

		private void addFriendAppBar_Click(object sender, RoutedEventArgs e)
		{
			this.Frame.Navigate(typeof(AddFriendPage));
		}

		private void editFriendsAppBar_Click(object sender, RoutedEventArgs e)
		{
			switchAppBars();
		}

		private async void deleteFriendsAppBar_Click(object sender, RoutedEventArgs e)
		{
			// For each selected friend we unfriend them, then send an update to the friend to remove the current user from the friends friendList if they are online
			// and then remove them from our friends list
			if (friendsListView.SelectedItems.Count > 0)
			{
				this.StartAsyncOperations("Removing friends...");
				foreach (FriendsListEntry friend in friendsListView.SelectedItems)
				{
					AppServices.operationComplete = false;
					AppServices.operationSuccessful = false;
					AppServices.buddyService.UnFriend(AppServices.localUsernameEncrypted, friend.encryptedUsername, new RemoveFriendCallback());

					await Task.Run(() =>
						{
							while (AppServices.operationComplete == false) ;
						});
					
					if (AppServices.operationSuccessful == false)
					{
						this.EndAsyncOperationsError();
						return;
					}

					// Send an update telling the other user to remove me from their friends list
					AppServices.appWarpLastSentToUsername = friend.username;
					if (friend.sessionEstablished)
						AppServices.warpClient.sendPrivateUpdate(friend.username, MessageBuilder.buildRemoveFriendBytes(friend.encryptionKey));
					else
						AppServices.warpClient.sendPrivateUpdate(friend.username, MessageBuilder.buildRemoveFriendBytes(AppServices.defaultEncryptionKey));

					//bool sendSuccessful = await AppServices.SendDataToUser(friend.username, MessageBuilder.buildRemoveFriendBytes());

					//if (sendSuccessful == false)
					//{
					//	this.EndAsyncOperationsError();
					//	return;
					//}

					AppServices.friendsData.friendsList.Remove(friend);
				}
				this.DisplayFriendsList();
				this.EndAsyncOperationsSuccess();
				friendsListView.SelectedItems.Clear();
				friendsListView.SelectedItem = null;
			}
		}

		public class RemoveFriendCallback : App42Callback
		{
			public RemoveFriendCallback()
			{
			}

			public void OnException(App42Exception exception)
			{
				AppServices.operationSuccessful = false;
				AppServices.errorMessage = "ERROR: unable to remove friend. Please check your network connection and try agian.";
				AppServices.operationComplete = true;
			}

			public void OnSuccess(object response)
			{
				AppServices.operationSuccessful = true;
				AppServices.operationComplete = true;
			}
		}

		private void RemoveFriendEntry(string friendName)
		{
			PrintMessage("removeFriendEntry function");
			return;
			int i = 0;
			foreach (FriendsListEntry friend in AppServices.friendsData.friendsList)
			{
				if (friend.username == friendName)
				{
					AppServices.friendsData.friendsList.RemoveAt(i);
					break;
				}
				i++;
			}
		}

		public async void RemoveFriendEntryAsync(string friendName)
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				RemoveFriendEntry(friendName);
			});
		}

		private void cancelEditAppBar_Click(object sender, RoutedEventArgs e)
		{
			switchAppBars();
		}

		// Switch to the editing App Bar so the user can remove a friend
		private void selectAppBar_Click(object sender, EventArgs e)
		{
			switchAppBars();
		}

		private void switchAppBars()
		{
			// Don't carry over chat buttons between switches
			if (lastSelected != null)
			{
				lastSelected.chatButtonsVisible = false;
			}

			if (friendsListView.SelectedItems.Count > 0)
			{
				friendsListView.SelectedItems.Clear();
			}

			// If we are showing the main app bar, switch to the editing mode
			if (deleteFriendsAppBar.Visibility == Windows.UI.Xaml.Visibility.Collapsed)
			{
				friendsListView.SelectionMode = ListViewSelectionMode.Multiple;

				addFriendAppBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				editFriendsAppBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

				deleteFriendsAppBar.Visibility = Windows.UI.Xaml.Visibility.Visible;
				cancelEditAppBar.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			else
			{
				friendsListView.SelectionMode = ListViewSelectionMode.Single;

				addFriendAppBar.Visibility = Windows.UI.Xaml.Visibility.Visible;
				editFriendsAppBar.Visibility = Windows.UI.Xaml.Visibility.Visible;

				deleteFriendsAppBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				cancelEditAppBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			}
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
				this.PrintMessage(message);
			});
		}

		private void friendsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (friendsListView.SelectionMode == ListViewSelectionMode.Single)
			{
				// If selected item is null (no selection) do nothing
				if (friendsListView.SelectedItem == null)
				{
					return;
				}

				// Retrieve the selected friend and toggle the chat buttons
				FriendsListEntry selectedFriend = friendsListView.SelectedItem as FriendsListEntry;

				// Setup the chat variables and open the chat window if we already have a session
				if (selectedFriend.sessionEstablished)
				{
					// Set the binding variables to the data from the current friend we are chatting with and hide the chat buttons
					AppServices.friendsData.currFriendsName = selectedFriend.username;
					AppServices.friendsData.chatHistory = selectedFriend.history;
					AppServices.friendsData.sendButtonEnabled = true;
					selectedFriend.chatButtonsVisible = false;
					lastSelected = null;
					friendsListView.SelectedItem = null;
					Frame.Navigate(typeof(ChatPage));
				}
				else
				{
					// Stop showing the chat buttons for the last selected friend. If the last selected and current selected are the same toggle the buttons
					//	and return
					if (lastSelected != null)
					{
						if (lastSelected.Equals(friendsListView.SelectedItem))
						{
							lastSelected.chatButtonsVisible = !lastSelected.chatButtonsVisible;
							friendsListView.SelectedItem = null;
							return;
						}
						else
						{
							lastSelected.chatButtonsVisible = false;
						}
					}


					selectedFriend.chatButtonsVisible = true;

					lastSelected = selectedFriend;
				}
				
				friendsListView.SelectedItem = null;
				return;
			}
			else if (friendsListView.SelectionMode == ListViewSelectionMode.Multiple)
			{
				return;
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


		private async void acceptRequestButton_Click(object sender, RoutedEventArgs e)
		{
			FriendRequest selectedRequest = null;
			FriendsListEntry newFriend = null;
			int i = 0;
			// Find the request which has it's buttons showing, this must be the one that has been pressed
			foreach (FriendRequest friendRequest in AppServices.friendsData.friendRequests)
			{
				if (friendRequest.showRequestButtons == Windows.UI.Xaml.Visibility.Visible)
				{
					selectedRequest = friendRequest;
					break;
				}
				i++;
			}
			// If a request exists then we accept it
			if (selectedRequest != null)
			{
				this.StartAsyncOperations("Accepting request...");
				AppServices.operationComplete = false;
				AppServices.buddyService.AcceptFriendRequest(AppServices.localUsernameEncrypted, selectedRequest.encryptedUsername, new AcceptRequestCallback());

				await Task.Run(() =>
					{
						while (AppServices.operationComplete == false) ;
					});

				// If the accept was successful we now remove them from the request list and add them to the friends list
				if (AppServices.operationSuccessful)
				{
					AppServices.friendsData.friendsList.Add(new FriendsListEntry { username = selectedRequest.username, isOnline = false });
					newFriend = AppServices.friendsData.friendsList.ElementAt(AppServices.friendsData.friendsList.Count);
					AppServices.friendsData.friendRequests.RemoveAt(i);

					// Update the friends list XAML in case this was the last request
					this.DisplayFriendRequestsList();

					// Now check if the new friend is online
					this.StartAsyncOperations("Checking if new friend is online...");
					AppServices.operationComplete = false;
					AppServices.isFriendOnline = false;
					AppServices.sessionService.GetSession(selectedRequest.encryptedUsername, false, new GetFriendSessionCallback(selectedRequest.encryptedUsername));

					await Task.Run(() =>
						{
							while (AppServices.operationComplete == false) ;
						});

					// An error occured and the friend may or may not be online. This is needed because the App42 callback will call the onException callback
					//	even if no error occured but the friend is offline. We just use isFriendOnline to either mark an error occuring if the operation
					//	wasn't successful. Or the friend's online status if the operation was successful
					if (AppServices.operationSuccessful == false && AppServices.isFriendOnline == true)
					{
						this.EndAsyncOperationsError();
						return;
					}
					// The friend is online
					if (AppServices.operationSuccessful && AppServices.isFriendOnline)
					{
						newFriend.isOnline = true;
						AppServices.appWarpLastSentToUsername = newFriend.username;
						AppServices.warpClient.sendPrivateUpdate(newFriend.username, MessageBuilder.buildAcceptedFriendRequestBytes(AppServices.defaultEncryptionKey));

						//bool sendSuccessful = await AppServices.SendDataToUser(selectedRequest.username, MessageBuilder.buildAcceptedFriendRequestBytes());

						//if (sendSuccessful == false)
						//{
						//	this.EndAsyncOperationsError();
						//	return;
						//}
					}
					// The friend is offline
					this.EndAsyncOperationsSuccess();
					return;
				}
				else
				{
					this.EndAsyncOperationsError();
					return;
				}
			}
		}

		private async void rejectRequestButton_Click(object sender, RoutedEventArgs e)
		{
			FriendRequest selectedRequest = null;
			int i = 0;
			// Find the request which has it's buttons showing, this must be the one that has been pressed
			foreach (FriendRequest friendRequest in AppServices.friendsData.friendRequests)
			{
				if (friendRequest.showRequestButtons == Windows.UI.Xaml.Visibility.Visible)
				{
					selectedRequest = friendRequest;
					break;
				}
				i++;
			}
			// If a request exists then we accept it
			if (selectedRequest != null)
			{
				this.StartAsyncOperations("Rejecting request...");
				AppServices.operationComplete = false;
				AppServices.buddyService.RejectFriendRequest(AppServices.localUsernameEncrypted, selectedRequest.encryptedUsername, new RejectRequestCallback(this, i));

				await Task.Run(() =>
					{
						while (AppServices.operationComplete == false) ;
					});

				// If the rejection is successful, remove them from the request list and return
				if (AppServices.operationSuccessful)
				{
					AppServices.friendsData.friendRequests.RemoveAt(i);
					this.DisplayFriendRequestsList();
					this.EndAsyncOperationsSuccess();
					return;
				}
				else
				{
					this.EndAsyncOperationsError();
					return;
				}
			}
		}

		public class RejectRequestCallback : App42Callback
		{

			private Page _page;
			private int _requestIndex;
			public RejectRequestCallback(MainPage currPage, int index)
			{
				_page = currPage;
				_requestIndex = index;
			}

			public void OnException(App42Exception exception)
			{
				int appErrorCode = exception.GetAppErrorCode();  
				int httpErrorCode = exception.GetHttpErrorCode();  
				if(appErrorCode == 4601)  
				{  
					//Handle here for Bad Request (The request parameters are invalid. Request already sent for user 'Nick')  
				}  
				else if(appErrorCode == 1401)  
				{  
					// handle here for Client is not authorized  
				}  
				else if(appErrorCode == 1500)  
				{  
					// handle here for Internal Server Error  
				}  

				AppServices.operationSuccessful = false;
				AppServices.operationComplete = true;
			}

			public void OnSuccess(object response)
			{
				AppServices.operationSuccessful = true;
				AppServices.operationComplete = true;
			}
		}

		private void requestsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// If selected item is null (no selection) do nothing
			if (requestsListView.SelectedItem == null)
			{
				return;
			}

			// Stop showing the chat buttons for the last selected friend. If the last selected and current selected are the same toggle the buttons
			//	and return
			if (lastSelectedRequest != null)
			{
				if (lastSelectedRequest.Equals(requestsListView.SelectedItem))
				{
					lastSelectedRequest.showRequestButtons = (lastSelectedRequest.showRequestButtons == Windows.UI.Xaml.Visibility.Visible) ? Windows.UI.Xaml.Visibility.Collapsed : Windows.UI.Xaml.Visibility.Visible;
					requestsListView.SelectedItem = null;
					return;
				}
				else
				{
					lastSelectedRequest.showRequestButtons = Windows.UI.Xaml.Visibility.Collapsed;
				}
			}

			// Retrieve the selected friend and toggle the chat buttons
			FriendRequest selectedRequest = requestsListView.SelectedItem as FriendRequest;
			selectedRequest.showRequestButtons = Windows.UI.Xaml.Visibility.Visible;

			lastSelectedRequest = selectedRequest;
			requestsListView.SelectedItem = null;
			return;
		}

		// Enable or disable the app bar depending on which pivot is selected
		private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			Pivot pivot = sender as Pivot;
			PivotItem selectedPivot = pivot.SelectedItem as PivotItem;
			if (selectedPivot.Equals(friendRequestsPivot))
			{
				editFriendsAppBar.IsEnabled = false;
			}
			else
			{
				editFriendsAppBar.IsEnabled = true;
			}
		}

		private void refreshAppBar_Click(object sender, RoutedEventArgs e)
		{
			this.SendStatusUpdatesToFriends(true);
			this.GetFriendsOnlineStatusAsync();
		}
		
		private void Page_Loaded(object sender, RoutedEventArgs e)
		{

		}

		private void Pivot_Loaded(object sender, RoutedEventArgs e)
		{

		}

		private void Pivot_GotFocus(object sender, RoutedEventArgs e)
		{

		}
	}

	// This callback class is used when only a single friend session is being checked so it needs to locks/mutex thread safety
	public class GetFriendSessionCallback : App42Callback
	{
		private Page _page;
		private string _friendUsername = string.Empty;

		public GetFriendSessionCallback()
		{
			_page = AppServices.friendsData._page;
		}

		public GetFriendSessionCallback(string friendUsername)
		{
			_friendUsername = friendUsername;
			_page = AppServices.friendsData._page;
		}

		// This will be called if there is an error or if there is no session. Error handling is currently not implemented we just assume
		//	that there is no session
		public void OnException(App42Exception exception)
		{
			int appErrorCode = exception.GetAppErrorCode();
			int httpErrorCode = exception.GetHttpErrorCode();
			if (appErrorCode == 2202)
			{
				// Handle here for Not Found (Session with the id '@sessionId' does not exist.) 
				AppServices.operationSuccessful = false;
				AppServices.errorMessage = "INPUT ERROR: User does not exist. Cannot check for session.";
				AppServices.isFriendOnline = true;
				AppServices.operationComplete = true;
				return;
			}
			// This is called when the user is simply offline. So it is not really an error.
			else if (appErrorCode == 2203)
			{
				// Handle here for Bad Request (The request parameters are invalid. Session with the Id '@sessionId' is already invalidated.) 
				AppServices.isFriendOnline = false;
			}
			else if (appErrorCode == 1401)
			{
				// handle here for Client is not authorized
				AppServices.operationSuccessful = false;
				AppServices.errorMessage = "NETWORK ERROR: Client not authorized to check for session.";
				AppServices.isFriendOnline = true;
				AppServices.operationComplete = true;
				return;
			}
			else if (appErrorCode == 1500)
			{
				// handle here for Internal Server Error  
				AppServices.operationSuccessful = false;
				AppServices.errorMessage = "NETWORK ERROR: An internal server error has occured while attempting check for a session.";
				AppServices.isFriendOnline = true;
				AppServices.operationComplete = true;
				return;
			}
			AppServices.operationSuccessful = true;
			AppServices.operationComplete = true;
		}

		public async void OnSuccess(object response)
		{
			// If the callback is called with a username we are trying to update an entry or add a new friend
			if (string.IsNullOrWhiteSpace(_friendUsername) == false)
			{
				Session friendSession = (Session)response;
				await AppServices.mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
				{
					FriendsListEntry myFriend = AppServices.friendsData.FindFriend(_friendUsername);
					if (myFriend == null)
					{
						AppServices.friendsData.friendsList.Add(new FriendsListEntry() { encryptedUsername = _friendUsername, isOnline = true, sessionID = friendSession.GetSessionId() });
					}
					else
					{
						myFriend.isOnline = true;
						myFriend.sessionID = friendSession.GetSessionId();
					}
				});
			}
			AppServices.operationSuccessful = true;
			AppServices.isFriendOnline = true;
			AppServices.operationComplete = true;
		}
	}


	// This Callback class is used when we are retrieving all the friends sessions during login/app launch so it has thread safety using a mutex
	public class GetFriendSessionsCallback : App42Callback
	{
		private Page _page;
		private string _friendUsername;
		public GetFriendSessionsCallback()
		{
			_page = AppServices.friendsData._page;
		}
		public GetFriendSessionsCallback(MainPage currPage)
		{
			_page = currPage;
		}
		public GetFriendSessionsCallback(string friendUsername)
		{
			_page = AppServices.friendsData._page;
			_friendUsername = friendUsername;
		}

		public void OnException(App42Exception exception)
		{
			App42Exception ex = (App42Exception)exception;
			int appErrorCode = exception.GetAppErrorCode();
			int httpErrorCode = exception.GetHttpErrorCode();

			// Attempt to get the mutex to update the number of friends sessions processed
			AppServices.friendCountMutex.WaitOne();
			AppServices.numFriendsProcessed++;

			// If all friends have been processed we signify the main thread we are done
			if (AppServices.numFriendsProcessed == AppServices.friendsData.friendsList.Count)
			{
				AppServices.friendCountMutex.ReleaseMutex();
				AppServices.operationSuccessful = true;
				AppServices.app42LoadedFriends = true;
				return;
			}

			AppServices.friendCountMutex.ReleaseMutex();
		}

		public async void OnSuccess(object response)
		{
			Session friendSession = (Session)response;
			await AppServices.friendsData._page.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
				{
					FriendsListEntry myFriend = AppServices.friendsData.FindFriend(_friendUsername);
					myFriend.isOnline = true;
					myFriend.sessionID = friendSession.GetSessionId();
				});

			// Attempt to get the mutex to update the number of friends sessions processed
			AppServices.friendCountMutex.WaitOne();
			AppServices.numFriendsProcessed++;
			AppServices.numOnlineFriends++;

			// If all friends have been processed we signify the main thread we are done
			if (AppServices.numFriendsProcessed == AppServices.friendsData.friendsList.Count)
			{
				AppServices.friendCountMutex.ReleaseMutex();
				AppServices.operationSuccessful = true;
				AppServices.app42LoadedFriends = true;
				return;
			}
			AppServices.friendCountMutex.ReleaseMutex();
		}
	}

	public class AcceptRequestCallback : App42Callback
	{
		public AcceptRequestCallback()
		{
		}

		public void OnException(App42Exception exception)
		{
			AppServices.operationSuccessful = false;
			AppServices.errorMessage = "ERROR: Unable to accept request. Please try again.";
			AppServices.operationComplete = true;
		}

		public void OnSuccess(object response)
		{
			AppServices.operationSuccessful = true;
			AppServices.operationComplete = true;
		}
	}
}
