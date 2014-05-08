using com.shephertz.app42.paas.sdk.windows;
using com.shephertz.app42.paas.sdk.windows.buddy;
using com.shephertz.app42.paas.sdk.windows.session;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Tap_Chat.ViewModel
{
	public class FriendsViewModel : INotifyPropertyChanged
	{
		public ObservableCollection<FriendsListEntry> friendsList { get; set; }
		public ObservableCollection<FriendRequest> friendRequests { get; set; }

		// This is essentially a list of strings, we bind to this in the XAML so that the chat list can be updated automatically when
		//	a new message is received
		public ObservableCollection<ChatEntry> chatHistory { get; set; }

		// Username of the friend we are currently chatting with
		public string currFriendsName { get; set; }

		private bool _sendButtonEnabled = true;
		public bool sendButtonEnabled 
		{ 
			get
			{
				return _sendButtonEnabled;
			}
			set
			{
				_sendButtonEnabled = value;
				this.RaisePropertyChanged("sendButtonEnabled");
			}
		}

		public string myUsername { get; set; }

		public Page _page;

		public FriendsViewModel()
		{
			chatHistory = new ObservableCollection<ChatEntry>();
			friendsList = new ObservableCollection<FriendsListEntry>();
		}

		public FriendsViewModel(MainPage currPage)
		{
			_page = currPage;
		}

		// Get all friends from App42 Cloud for the local user and populate the friendsList
		public void GetFriendsFromCloud()
		{
			if (friendsList == null)
				friendsList = new ObservableCollection<FriendsListEntry>();

			AppServices.buddyService.GetAllFriends(AppServices.localUsernameEncrypted, new GetFriendsCallback(this));
			return;
		}

		// Get all the pending friend requests from the App42 cloud for the local user and populate the request list
		public void GetFriendRequestsFromCloud()
		{
			// If the request list is uninitialized, create it.
			if (friendRequests == null)
				friendRequests = new ObservableCollection<FriendRequest>();
			// Each time the list is retrieved, clear the list
			if (friendRequests.Count > 0)
				friendRequests.Clear();

			AppServices.buddyService.GetFriendRequest(AppServices.localUsernameEncrypted, new GetRequestsCallback(this));
		}

		// Find a friend in the friends list by username or encrypted username
		public FriendsListEntry FindFriend(string friendName)
		{
			foreach (FriendsListEntry friend in this.friendsList)
			{
				if (friend.username.Equals(friendName) || friend.encryptedUsername.Equals(friendName))
					return friend;
			}

			return null;
		}

		public void UpdateOnlineStatus(string friendName, bool newStatus)
		{
			FriendsListEntry friend = FindFriend(friendName);
			if (friend != null)
				friend.isOnline = newStatus;
		}

		public class GetFriendsCallback : App42Callback
		{
			FriendsViewModel _friendsModel;

			public GetFriendsCallback(FriendsViewModel currModel)
			{
				_friendsModel = currModel;
			}

			public void OnException(App42Exception exception)
			{
				int appErrorCode = exception.GetAppErrorCode();
				int httpErrorCode = exception.GetHttpErrorCode();

				string errorMsg = string.Empty;
				switch (appErrorCode)
				{
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
					case 4604:
						// User has no friends yet so the App42 API's return this error code. This will always happen for new users
						//	and user's with empty friends lists but it is not really an error
						AppServices.operationSuccessful = true;
						AppServices.app42LoadedFriends = true;
						return;
					case 4613:
						errorMsg = "USAGE ERROR: The entered username is already on your friends list. Please add a different friend.";
						break;
					default:
						errorMsg = "ERROR: An error has occured. Please check your network connection and username entry and try again.";
						break;
				}

				AppServices.operationSuccessful = false;
				AppServices.errorMessage = errorMsg;
				AppServices.app42LoadedFriends = true;

				//_friendsModel._page.SetProgressIndicatorAsync(false, string.Empty);
				//_friendsModel._page.EnableInputAsync();
				//_friendsModel._page.PrintMessage(errorMsg);
			}

			// We have successfully retrieved the buddy list. Now add each friend to the friendsList
			public async void OnSuccess(object response)
			{
				List<Buddy> buddyList = (List<Buddy>)response;

				foreach (Buddy friend in buddyList)
				{
					// Because the friendsList is an observable collection we can only modify it in the Main UI thread because it will call
					//	a NotifyPropertyChanged event every time somthing is added and this can only be handled in the UI thread
					await _friendsModel._page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							AppServices.friendsData.friendsList.Add(new FriendsListEntry { encryptedUsername = friend.GetBuddyName(), isOnline = false });
						});
				}

				AppServices.operationSuccessful = true;
				AppServices.app42LoadedFriends = true;
			}
		}

		public class GetRequestsCallback : App42Callback
		{
			FriendsViewModel _friendsModel;

			public GetRequestsCallback(FriendsViewModel currModel)
			{
				_friendsModel = currModel;
			}

			public void OnException(App42Exception exception)
			{
				int appErrorCode = exception.GetAppErrorCode();
				int httpErrorCode = exception.GetHttpErrorCode();

				string errorMsg = string.Empty;
				switch (appErrorCode)
				{
					case 4601:
						errorMsg = "ERROR: Friend request already sent for this user. Please add a different friend.";
						break;					
					case 1401:
						errorMsg = "NETWORK ERROR: Client not Authorized. Please try again later.";
						break;
					case 1500:
						errorMsg = "NETWORK ERROR: A server error has occured. Please try again later.";
						break;
					default:
						// This is called when there are no requests for the user or the user has never used the buddy service yet
						//	so it is not an error and we set the OperationSuccessful to true
						AppServices.operationSuccessful = true;
						AppServices.app42LoadedRequests = true;
						return;
				}

				//_friendsModel._page.SetProgressIndicatorAsync(false, string.Empty);
				//_friendsModel._page.DisplayFriendRequestsListAsync();
				//_friendsModel._page.EnableInputAsync();

				AppServices.operationSuccessful = false;
				AppServices.errorMessage = errorMsg;
				AppServices.app42LoadedRequests = true;
			}

			public async void OnSuccess(object response)
			{
				List<Buddy> buddyList = (List<Buddy>)response;

				await _friendsModel._page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
					{
						foreach (Buddy friend in buddyList)
						{
							AppServices.friendsData.friendRequests.Add(new FriendRequest() { encryptedUsername = friend.GetBuddyName() });
						}
					});

				AppServices.operationSuccessful = true;
				AppServices.app42LoadedRequests = true;

				//_friendsModel._page.SetProgressIndicatorAsync(false, string.Empty);
				//_friendsModel._page.DisplayFriendRequestsListAsync();
				//_friendsModel._page.EnableInputAsync();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void RaisePropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}

	
}
