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
using com.shephertz.app42.paas.sdk.windows;
using Windows.UI.Core;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace Tap_Chat
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class AddFriendPage : Page
	{
		private NavigationHelper navigationHelper;
		private ObservableDictionary defaultViewModel = new ObservableDictionary();
		private bool inputEnabled = true;
		private long publishedMessageID = -1;
		private long subscribedMessageID = -1;

		private string nfcFriendName = string.Empty;
		private string startNFCMessageType = "Windows.TapChatFriendRequestStart";
		private string friendRequestSentNFCMessageType = "Windows.TapChatFriendRequestSent";
		private string friendRequestCompleteNFCMessageType = "Windows.TapChatFriendRequestComplete";
		private string newEncryptionKey = string.Empty;
		private IBuffer keyBuff = null;

		public AddFriendPage()
		{
			this.InitializeComponent();

			this.navigationHelper = new NavigationHelper(this);
			this.navigationHelper.LoadState += this.NavigationHelper_LoadState;
			this.navigationHelper.SaveState += this.NavigationHelper_SaveState;

			// Get the NFC chip for this device
			this.InitializeProximityDevice();
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
			// Ensure we no longer use proximity when leaving the page
			this.ProximityDeviceDeparted(null);
			AppServices.myProximityDevice.DeviceArrived -= ProximityDeviceArrived;
			AppServices.myProximityDevice.DeviceDeparted -= ProximityDeviceDeparted;
		}

		private void InitializeProximityDevice()
		{
			AppServices.myProximityDevice = Windows.Networking.Proximity.ProximityDevice.GetDefault();

			if (AppServices.myProximityDevice == null)
				this.PrintMessage("Unable to get NFC chip. This device might not be capable of proximity interactions or NFC is disabled in settings");
			else
			{
				AppServices.myProximityDevice.DeviceArrived += ProximityDeviceArrived;
				AppServices.myProximityDevice.DeviceDeparted += ProximityDeviceDeparted;
			}

		}

		private void ProximityDeviceArrived(object sender)
		{
			this.subscribedMessageID = AppServices.myProximityDevice.SubscribeForMessage(this.startNFCMessageType, ReceiveUsernameNFC);
			this.publishedMessageID = AppServices.myProximityDevice.PublishMessage(this.startNFCMessageType, AppServices.localUsernameEncrypted);
		}

		private async void ReceiveUsernameNFC(Windows.Networking.Proximity.ProximityDevice device, Windows.Networking.Proximity.ProximityMessage message)
		{
			// Once a message is received stop subscribing for it
			AppServices.myProximityDevice.StopSubscribingForMessage(this.subscribedMessageID);
			this.subscribedMessageID = -1;
			
			// Check to see who's username is greater. The one with the greater username sends the request and generates a key
			int compareResult = string.Compare(AppServices.localUsernameEncrypted, message.DataAsString);
			this.nfcFriendName = message.DataAsString;

			// I must send friend request using the App42 API
			if (compareResult > 0)
			{
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
					{
						this.StartAsyncOperations("Establishing friendship...");
					});
				AppServices.operationComplete = false;
				AppServices.buddyService.SendFriendRequest(AppServices.localUsernameEncrypted, message.DataAsString, "Friend Request", new FriendRequestCallback());

				//await Task.Run(() =>
				//{
					while (AppServices.operationComplete == false) ;
				//});

				// Once the friend request is sent completely we stop publishing our name
				AppServices.myProximityDevice.StopPublishingMessage(this.publishedMessageID);
				this.publishedMessageID = -1;

				// If sending the request failed we notify the other user
				if (AppServices.operationSuccessful == false)
				{
					this.publishedMessageID = AppServices.myProximityDevice.PublishMessage(this.friendRequestSentNFCMessageType, "false");
					await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
					{
						this.EndAsyncOperationsError();
					});
					return;
				}
				// The send was successful so we tell the other user our encryption key and now wait for them to accept and notify us
				keyBuff = CryptographicBuffer.GenerateRandom(256);
				this.newEncryptionKey = CryptographicBuffer.EncodeToHexString(keyBuff);
				this.publishedMessageID = AppServices.myProximityDevice.PublishMessage(this.friendRequestSentNFCMessageType, this.newEncryptionKey);
				this.subscribedMessageID = AppServices.myProximityDevice.SubscribeForMessage(this.friendRequestCompleteNFCMessageType, CompleteFriendshipNFC);
			}
			// I must wait for a friend request from the other user using the App42 API
			else
			{
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
					{
						this.StartAsyncOperations("Waiting for friendship data...");
					});
				this.subscribedMessageID = AppServices.myProximityDevice.SubscribeForMessage(this.friendRequestSentNFCMessageType, AcceptFriendRequestNFC);
				AppServices.myProximityDevice.StopPublishingMessage(this.publishedMessageID);
				this.publishedMessageID = -1;
			}
		}

		private async void AcceptFriendRequestNFC(Windows.Networking.Proximity.ProximityDevice sender, Windows.Networking.Proximity.ProximityMessage message)
		{
			// Stop subscribing to the message. This means the other user finished sending a friend request
			AppServices.myProximityDevice.StopSubscribingForMessage(this.subscribedMessageID);
			this.subscribedMessageID = -1;

			// Other user successfully sent friend request (message is not "false"), so we receive the generated encryption key
			if (message.DataAsString.Equals("false") == false)
			{
				// Accept the friend request
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
						{
							this.StartAsyncOperations("Accepting friend request...");
						});
				AppServices.operationComplete = false;
				AppServices.buddyService.AcceptFriendRequest(AppServices.localUsernameEncrypted, this.nfcFriendName, new AcceptRequestCallback());

				//await Task.Run(() =>
				//{
				while (AppServices.operationComplete == false) ;
				//});

				// Unable to accept friend request. Error out and tell the other user
				if (AppServices.operationSuccessful == false)
				{
					this.publishedMessageID = AppServices.myProximityDevice.PublishMessage(this.friendRequestCompleteNFCMessageType, "false");
					await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
						{
							this.EndAsyncOperationsError();
						});
					return;
				}

				// Accepted friend request successfully. Tell the other user and add them to our friends list
				this.publishedMessageID = AppServices.myProximityDevice.PublishMessage(this.friendRequestCompleteNFCMessageType, "true");
				this.newEncryptionKey = message.DataAsString;
				IBuffer keyBuff = CryptographicBuffer.DecodeFromHexString(this.newEncryptionKey);
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
					{
						AppServices.friendsData.friendsList.Add(new Tap_Chat.ViewModel.FriendsListEntry() { encryptedUsername = this.nfcFriendName, isOnline = true, encryptionKey = AppServices.encryptionAlgorithim.CreateSymmetricKey(keyBuff) });
						this.EndAsyncOperationsSuccess();
						this.PrintMessage("Added Friend Successfully!");
					});
			}
			// Other user had an issue sending a request so we print a message and end
			else
			{
				// We ended in success but the other user had an error so we hide the progress indicator and print a different message
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
						{
							this.EndAsyncOperationsSuccess();
						});
				this.PrintMessageAsync("Other user had an error. Unable to add friend.");
				return;
			}
		}

		private async void CompleteFriendshipNFC(Windows.Networking.Proximity.ProximityDevice sender, Windows.Networking.Proximity.ProximityMessage message)
		{
			// Stop subscribing to the message. This means the other user finished sending a friend request
			AppServices.myProximityDevice.StopSubscribingForMessage(this.subscribedMessageID);
			this.subscribedMessageID = -1;

			// Stop publishing friend request sent message as the user has already processed it
			AppServices.myProximityDevice.StopPublishingMessage(this.publishedMessageID);
			this.publishedMessageID = -1;

			// Other user accepted our friend request successfully so we add them to our list and save the encryption key
			if (message.DataAsString.Equals("true"))
			{
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
					{
						AppServices.friendsData.friendsList.Add(new Tap_Chat.ViewModel.FriendsListEntry() { encryptedUsername = this.nfcFriendName, isOnline = true, encryptionKey = AppServices.encryptionAlgorithim.CreateSymmetricKey(keyBuff) });
						this.EndAsyncOperationsSuccess();
						this.PrintMessage("Added Friend Successfully!");
					});
			}
			// Other user failed to accept our friend request so we print out a message and end
			else
			{
				await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
					{
						// Even though the add failed we call success to remove the progress indicator and enable input
						this.EndAsyncOperationsSuccess();
						this.PrintMessage("Other user had an error. Add friend failed");
					});
			}
		}

		private void ProximityDeviceDeparted(object sender)
		{
			if (this.publishedMessageID != -1)
				AppServices.myProximityDevice.StopPublishingMessage(this.publishedMessageID);
			if (this.subscribedMessageID != -1)
				AppServices.myProximityDevice.StopSubscribingForMessage(this.subscribedMessageID);
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

		private async void addFriendButton_Click(object sender, RoutedEventArgs e)
		{
			this.StartAsyncOperations("Sending friend request...");
			string friendNameEncrypted = AppServices.EncryptString(friendUsernameBox.Text, AppServices.secretKey);
			AppServices.operationComplete = false;
			AppServices.buddyService.SendFriendRequest(AppServices.localUsernameEncrypted, friendNameEncrypted, "Friend Request", new FriendRequestCallback());

			await Task.Run(() =>
				{
					while (AppServices.operationComplete == false) ;
				});

			if (AppServices.operationSuccessful == false)
			{
				this.EndAsyncOperationsError();
				return;
			}

			// Once the friend request has been sent successfully we check if the user is online, and then send them an update to say they have a request
			AppServices.operationComplete = false;
			AppServices.isFriendOnline = false;
			this.StartAsyncOperations("Sending friend request...");

			AppServices.sessionService.GetSession(friendNameEncrypted, false, new GetFriendSessionCallback());

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
			// The friend is online so send them an update saying there is a new friend request
			if (AppServices.operationSuccessful && AppServices.isFriendOnline)
			{
				AppServices.appWarpLastSentToUsername = friendUsernameBox.Text;
				AppServices.warpClient.sendPrivateUpdate(friendUsernameBox.Text, MessageBuilder.buildNewFriendRequestBytes(AppServices.defaultEncryptionKey));
				//this.StartAsyncOperations("Sending friend request...");

				//bool sendSuccessful = await AppServices.SendDataToUser(friendUsernameBox.Text, MessageBuilder.buildNewFriendRequestBytes());

				//if (sendSuccessful == false)
				//{
				//	this.EndAsyncOperationsError();
				///	return;
				//}
			}

			// The friend is offline
			this.EndAsyncOperationsSuccess();
			return;
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
			entryGrid.IsTapEnabled = false;
			entryGrid.Opacity = 0.2;
		}

		public void EnableInput()
		{
			entryGrid.IsTapEnabled = true;
			entryGrid.Opacity = 1.0;
		}

		public async void EnableInputAsync()
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				entryGrid.IsTapEnabled = true;
				entryGrid.Opacity = 1.0;
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

		public async void SendFriendRequestCompleteAsync()
		{
			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				SetProgressIndicator(false, string.Empty);
				EnableInput();
				PrintMessage("Friend Request Sent Successfully to " + friendUsernameBox.Text + "!");
			});
		}


		public class FriendRequestCallback : App42Callback
		{
			public FriendRequestCallback( )
			{
			}

			public void OnException(App42Exception exception)
			{
				int appErrorCode = exception.GetAppErrorCode();
				int httpErrorCode = exception.GetHttpErrorCode();

				string errorMsg = string.Empty;
				switch (appErrorCode)
				{
					case 0:
						errorMsg = "ENTRY ERROR: Username cannot be blank. Please try again.";
						break;
					case 4600:
						errorMsg = "ENTRY ERROR: Username entered does not exist. Please try again.";
						break;
					case 4601:
						errorMsg = "USAGE ERROR: Friend request already sent for this user. Please add a different friend.";
						break;
					case 4602:
						errorMsg = "ENTRY ERROR: Username entered does not exist. Please try again.";
						break;
					case 4603:
						errorMsg = "ENTRY ERROR: Username entered does not exist. Please try again.";
						break;
					case 4613:
						errorMsg = "USAGE ERROR: The entered username is already on your friends list. Please add a different friend.";
						break;
					default:
						errorMsg = "ERROR: An error has occured. Please check your network connection and username entry and try again.";
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
	}
}
