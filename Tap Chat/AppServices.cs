using com.shephertz.app42.gaming.multiplayer.client;
using com.shephertz.app42.gaming.multiplayer.client.command;
using com.shephertz.app42.paas.sdk.windows;
using com.shephertz.app42.paas.sdk.windows.buddy;
using com.shephertz.app42.paas.sdk.windows.session;
using com.shephertz.app42.paas.sdk.windows.storage;
using com.shephertz.app42.paas.sdk.windows.user;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tap_Chat.ViewModel;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Tap_Chat
{
	public enum appWarpConnectModes { justConnect, sendAllUpdates };

	// This class contains global variables and functions/classes that are used in the App42 API calls as well as some data needed on more than one page
	public static class AppServices
	{
		public static string apiKey = "c2278cbbe736aa1ef990851647824fd6a7043a86693f21e30fdb1d51def9f048";
		public static string secretKey = "871faafc04f21dc92e592022874437a37fdee097387ac28996c15dfd355f1fb4";
		public static SymmetricKeyAlgorithmProvider encryptionAlgorithim = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesEcbPkcs7);
		public static CryptographicKey defaultEncryptionKey;
		
		public static ServiceAPI app42Api = new ServiceAPI(apiKey, secretKey);
		public static UserService userService = app42Api.BuildUserService();
		public static SessionService sessionService = app42Api.BuildSessionService();
		public static BuddyService buddyService = app42Api.BuildBuddyService();
		public static StorageService storageService = app42Api.BuildStorageService();

		public static MainPage mainPage;

		public static string localUsername;
		public static string localSessionId;
		public static string localIP;

		/* Status variables to indicate when worker threads have completed work so the main thread can continue */
		public static appWarpConnectModes appWarpConnectMode = appWarpConnectModes.sendAllUpdates;

		public static bool appWarpLoggedIn = false;				// Inidicates whether the AppWarp connect call back has completed
		public static bool appWarpUpdateSent = false;
		public static string appWarpLastSentToUsername;

		public static bool app42Authenticated = false;			// Indicates whether App42 Authenticate has completed
		public static bool app42LoggedIn = false;				// Inidicates whether App42 GetSession has completed
		public static bool app42LoadedFriends = false;			// Indicates whether App42 GetFriends has completed
		public static bool app42LoadedRequests = false;			// Indicates whether App42 GetFriendRequests has completed

		// This is used when getting the session for a single friend since it will return an exception if the friend is offline and this way we can
		//	detect that as not an error
		public static bool isFriendOnline = false;			

		public static bool operationComplete = false;

		public static bool operationSuccessful = false;			// Indicates if a call back was successful
		public static string errorMessage = string.Empty;		// Contains the error message of a call back if it fails

		public static Mutex friendCountMutex = new Mutex(false);
		public static int numFriendsProcessed = 0;
		public static int numOnlineFriends = 0;

		public static int maxAttempts = 3;
		public static int connectAttempts = 0;

		public static string localUsernameEncrypted;

		public static WarpClient warpClient;
		public static ConnectionListener connListenObj = new ConnectionListener();
		public static NotificationListener notificationListnerObj = new NotificationListener();
		public static UpdateListener updateListenerObj = new UpdateListener();

		public static int appWarpRecoveryTime = 10;

		public static FriendsViewModel friendsData = new FriendsViewModel();

		public static Windows.Networking.Proximity.ProximityDevice myProximityDevice;

		// Encrypt data using the AES_ECB_PKCS7 Algorithim and the given key and return the encrypted data as a Hex encoded string
		public static string EncryptString(string data, string encryptionKey)
		{
			// Create the algorithim, the key, and a buffer to store the string that we want to encrypt
			IBuffer keyBuff = CryptographicBuffer.ConvertStringToBinary(encryptionKey, BinaryStringEncoding.Utf8);
			CryptographicKey key = encryptionAlgorithim.CreateSymmetricKey(keyBuff);
			IBuffer dataBuff = CryptographicBuffer.ConvertStringToBinary(data, BinaryStringEncoding.Utf8);

			// Encrypt the data into a buffer and then return a string represetnation of that buffer. There is no Initialization
			//	vector needed when using ECB so it is set to null
			IBuffer encryptedBuff = CryptographicEngine.Encrypt(key, dataBuff, null);
			return CryptographicBuffer.EncodeToHexString(encryptedBuff);
		}

		// Encrypt data using the AES_ECB_PKCS7 Algorithim and the given key and return the encrypted data as a Hex encoded string
		public static string EncryptString(string data, CryptographicKey encryptionKey)
		{
			// Create the algorithim, the key, and a buffer to store the string that we want to encrypt
			IBuffer dataBuff = CryptographicBuffer.ConvertStringToBinary(data, BinaryStringEncoding.Utf8);

			// Encrypt the data into a buffer and then return a string represetnation of that buffer. There is no Initialization
			//	vector needed when using ECB so it is set to null
			IBuffer encryptedBuff = CryptographicEngine.Encrypt(encryptionKey, dataBuff, null);
			return CryptographicBuffer.EncodeToHexString(encryptedBuff);
		}

		// Decrypt data using the AES_ECB_PKCS7 Algorithim and the given key and return the raw data as a UTF8 encoded string.
		//	the data passed in should be encrypted HEX encoded string that was encrypted using the passed in encryptionKey
		public static string DecryptString(string data, string encryptionKey)
		{
			// Create the algorithim, the key, and a buffer to store the string that we want to encrypt
			IBuffer keyBuff = CryptographicBuffer.ConvertStringToBinary(encryptionKey, BinaryStringEncoding.Utf8);
			CryptographicKey key = encryptionAlgorithim.CreateSymmetricKey(keyBuff);
			IBuffer dataBuff = CryptographicBuffer.DecodeFromHexString(data);

			// Decrypt the data and return it as a string
			IBuffer decryptedBuff = CryptographicEngine.Decrypt(key, dataBuff, null);
			return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decryptedBuff);
		}

		// Decrypt data using the AES_ECB_PKCS7 Algorithim and the given key and return the raw data as a UTF8 encoded string.
		//	the data passed in should be encrypted HEX encoded string that was encrypted using the passed in encryptionKey
		public static string DecryptString(string data, CryptographicKey encryptionKey)
		{
			// Create the algorithim, the key, and a buffer to store the string that we want to encrypt
			IBuffer dataBuff = CryptographicBuffer.DecodeFromHexString(data);

			// Decrypt the data and return it as a string
			IBuffer decryptedBuff = CryptographicEngine.Decrypt(encryptionKey, dataBuff, null);
			return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decryptedBuff);
		}

		public static void logout()
		{
			if (AppServices.appWarpLoggedIn)
			{
				// Tell all online friends I am offline
				foreach (FriendsListEntry friend in AppServices.friendsData.friendsList)
				{
					if (friend.isOnline)
					{
						AppServices.appWarpLastSentToUsername = friend.username;
						if (friend.sessionEstablished)
							AppServices.warpClient.sendPrivateUpdate(friend.username, MessageBuilder.buildOnlineStatusUpdateBytes(false, friend.encryptionKey));
						else
							AppServices.warpClient.sendPrivateUpdate(friend.username, MessageBuilder.buildOnlineStatusUpdateBytes(false, AppServices.defaultEncryptionKey));
					}
				}

				// Logout of AppWarp
				AppServices.warpClient.Disconnect();
			}

			if (AppServices.app42LoggedIn)
			{
				// Logout of App42 Services
				AppServices.sessionService.Invalidate(AppServices.localSessionId, new LogoutSessionCallback());
				AppServices.localSessionId = string.Empty;
			}

			AppServices.resetStatusVaribles();
		}

		public static void resetStatusVaribles()
		{
			AppServices.appWarpLoggedIn = false;				
			AppServices.app42Authenticated = false;			
			AppServices.app42LoggedIn = false;				
			AppServices.app42LoadedFriends = false;			
			AppServices.app42LoadedRequests = false;			
			AppServices.appWarpUpdateSent = false;
		}

		public async static Task<bool> SendDataToUser(string sendTo, byte[] data)
		{
			if (WarpClient.GetInstance().GetConnectionState() != 1)
			{
				AppServices.errorMessage = "Can't send update, not connected to AppWarp";
				return false;
			}
			int maxAttempts = 3;
			int attempts;
			for (attempts = 1; attempts <= maxAttempts; attempts++)
			{
				AppServices.appWarpUpdateSent = false;
				AppServices.errorMessage = string.Empty;
				AppServices.appWarpLastSentToUsername = sendTo;

				AppServices.warpClient.sendPrivateUpdate(sendTo, data);

				// Wait on the first round. For some reason the SendPrivateUpdate callback will never be called the first time but
				//	it will also always fail if you simply try to call it a 2nd time immediately. So we wait 200ms (a safe number
				//	found just via trial and error seeing which values worked) the first time we call the API and then afterwards
				//	we simply wait for the variable to be set as usual
				if (attempts == 0)
				{
					// The next few lines essentially causes the main thread to wait for a specified period of time
					AppServices.friendCountMutex.WaitOne();

					await Task.Run(() =>
					{
						//while (AppServices.appWarpUpdateSent == false) ;
						AppServices.friendCountMutex.WaitOne(200);
					});

					AppServices.friendCountMutex.ReleaseMutex();
				}
				else
				{
					await Task.Run(() =>
					{
						while (AppServices.appWarpUpdateSent == false) ;
					});
				}

				// Send for this friend was successful so end
				if (AppServices.operationSuccessful)
				{
					return true;
				}
			}

			return false;
		}

		public static bool SendDataToUserSync(string sendTo, byte[] data)
		{
			//if (WarpClient.GetInstance().GetConnectionState() != 1)
			//{
			//	AppServices.errorMessage = "Can't send update, not connected to AppWarp";
			//	return false;
			//}
			int maxAttempts = 1;
			int attempts;
			for (attempts = 1; attempts <= maxAttempts; attempts++)
			{
				AppServices.appWarpUpdateSent = false;
				AppServices.errorMessage = string.Empty;
				AppServices.operationSuccessful = false;
				AppServices.appWarpLastSentToUsername = sendTo;

				AppServices.warpClient.sendPrivateUpdate(sendTo, data);

				// The next few lines essentially causes the main thread to wait for a specified period of time
				//AppServices.friendCountMutex.WaitOne();

				Task.Run(() =>
				{
					//while (AppServices.appWarpUpdateSent == false) ;
					AppServices.friendCountMutex.WaitOne(600);
				}).Wait();

				//AppServices.friendCountMutex.ReleaseMutex();

				//while (AppServices.appWarpUpdateSent == false) ;

				// Send for this friend was successful so end
				if (AppServices.operationSuccessful)
				{
					return true;
				}
			}

			return false;
		}
	}

	public class LogoutSessionCallback : App42Callback
	{

		public void OnException(App42Exception exception)
		{

		}

		public void OnSuccess(object response)
		{

		}
	}

	public class ConnectionListener : com.shephertz.app42.gaming.multiplayer.client.listener.ConnectionRequestListener
	{
		public MainPage _page;

		public ConnectionListener()
		{

		}

		public ConnectionListener(MainPage currPage)
		{
			_page = currPage;
		}

		// This will be called after the user presses Login on the LoginPage and a session has been established using the App42 cloud API's
		//	this then connects and creates a session with the AppWarp API's which will be used for initial chat establishment and update
		//	messages for things like online status and message types.
		public void onConnectDone(com.shephertz.app42.gaming.multiplayer.client.events.ConnectEvent eventObj)
		{
			byte result = eventObj.getResult();
			string errorMsg = string.Empty;

			// If the connection was successful check which mode the call was made in and do the desired operation
			if (result == WarpResponseResultCode.SUCCESS)
			{
				AppServices.operationSuccessful = true;

				if (AppServices.appWarpConnectMode == appWarpConnectModes.sendAllUpdates)
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
				AppServices.appWarpConnectMode = appWarpConnectModes.justConnect;
				AppServices.appWarpLoggedIn = true;
				return;
			}
			else if (result == WarpResponseResultCode.CONNECTION_ERROR_RECOVERABLE)
			{
				AppServices.warpClient.RecoverConnection();
				return;
			}
			else if (result == WarpResponseResultCode.SUCCESS_RECOVERED)
			{
				return;
			}
			else if (result == WarpResponseResultCode.AUTH_ERROR)
			{
				errorMsg = "Connect Authentication Error: Attempting to connect without initializing the API keys.";
			}
			else if (result == WarpResponseResultCode.BAD_REQUEST)
			{
				errorMsg = "Bad Request: The entered username does not exist.";
			}
			else if (result == WarpResponseResultCode.CONNECTION_ERR)
			{
				errorMsg = "Network Connection Error: Please check your network connection and try again.";
			}
			else
			{
				errorMsg = "Unknown Error: Please check your network connection and the entered username and try again.";
			}

			AppServices.operationSuccessful = false;
			AppServices.errorMessage = errorMsg;
			AppServices.appWarpLoggedIn = true;
		}

		public void onDisconnectDone(com.shephertz.app42.gaming.multiplayer.client.events.ConnectEvent eventObj)
		{

		}


		public void onInitUDPDone(byte resultCode)
		{

		}
	}

	public class NotificationListener : com.shephertz.app42.gaming.multiplayer.client.listener.NotifyListener
	{
		public Page _page;

		public NotificationListener()
		{

		}

		public NotificationListener(MainPage currPage)
		{
			_page = currPage;
		}
		
		public void onChatReceived(com.shephertz.app42.gaming.multiplayer.client.events.ChatEvent eventObj)
		{
			throw new NotImplementedException();
		}

		public void onGameStarted(string sender, string roomId, string nextTurn)
		{
			throw new NotImplementedException();
		}

		public void onGameStopped(string sender, string roomId)
		{
			throw new NotImplementedException();
		}

		public void onMoveCompleted(com.shephertz.app42.gaming.multiplayer.client.events.MoveEvent moveEvent)
		{
			throw new NotImplementedException();
		}

		public void onPrivateChatReceived(string sender, string message)
		{
			throw new NotImplementedException();
		}

		/* This function will be used to send JSON objects (JObjects) with the following format Keys:
		 * string updateType: This will be one of the following choices:
		 *		"chat" if we are receiving a new chat message.
		 *		"onlineStatus" if a friend is notifying us of an online status change
		 *		"ipInfo" if this contains information about the friends IP address and port
		 *		"request" if this contains information about a new friend request or update to a friend request or friend relationship
		 *	
		 * var data: This will be different types of data depending on the update type:
		 *		"chat" - this will be a string containing the message
		 *		"onlineStatus" - this will be a bool onlineStatus and a string sessionID
		 *		"ipInfo" - this will be 2 strings ipAddr and port.
		 *		"request" - this will be a string requestStatus, if we have no pending requests already from the sender (either ones we sent or received) 
		 *			then this is a new friend request and the string will be "new". If a friend accepted our request this will be "accepted" 
		 *			If a friend removed us from their list then this will be "removed"
		 */
		public async void onPrivateUpdateReceived(string sender, byte[] update, bool fromUdp)
		{
			FriendsListEntry sendingFriend = AppServices.friendsData.FindFriend(sender);
			JObject updateData;
			// Decrypt the data using the session key if we have one already or the default if we have no session with the sender
			if (sendingFriend != null && sendingFriend.sessionEstablished)
				updateData = MessageBuilder.decodeMessageBytes(update, sendingFriend.encryptionKey);
			else
				updateData = MessageBuilder.decodeMessageBytes(update, AppServices.defaultEncryptionKey);
			string updateType = updateData["updateType"].ToString();
			if (updateType.Equals("chat"))
			{
				string chatMessage = updateData["message"].ToString();
				// The currently open chat is the one we are receiving a message from we do not need to update the unread count
				if (sender.Equals(AppServices.friendsData.currFriendsName))
				{
					await AppServices.friendsData._page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							FriendsListEntry currFriend = AppServices.friendsData.FindFriend(sender);
							currFriend.history.Add(new ChatEntry() { message = chatMessage, sender = sender });
						});
				}
				else
				{
					await AppServices.friendsData._page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							FriendsListEntry currFriend = AppServices.friendsData.FindFriend(sender);
							int currUnreadCount = System.Convert.ToInt32(currFriend.unreadCount);
							currUnreadCount++;
							currFriend.unreadCount = currUnreadCount.ToString();
							currFriend.history.Add(new ChatEntry() { message = chatMessage, sender = sender });
						});

				}
			}
			// A friend has sent a change in their online status so we get the data and update the friendsList in the ViewModel
			else if (updateType.Equals("onlineStatus"))
			{
				bool friendOnlineStatus = (bool)JsonConvert.DeserializeObject(updateData["onlineStatus"].ToString(), typeof(bool));
				string friendSessionID = updateData["sessionID"].ToString();
				//_page.PrintMessageAsync("Received new status update\n" + "Sender: " + sender + "\nOnline: " + friendOnlineStatus.ToString());

				await AppServices.friendsData._page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
					{
						FriendsListEntry friend = AppServices.friendsData.FindFriend(sender);
						friend.isOnline = friendOnlineStatus;
						friend.sessionID = friendSessionID;
						// If we are chatting with the current user and they logout then we disable the send button
						if (AppServices.friendsData.currFriendsName != null && AppServices.friendsData.currFriendsName.Equals(friend.username))
						{
							if (friendOnlineStatus == false)
								AppServices.friendsData.sendButtonEnabled = false;
						}
					});				
			}
			else if (updateType.Equals("ipInfo"))
			{

			}
			else if (updateType.Equals("request"))
			{
				string requestStatus = updateData["requestStatus"].ToString();
				// This is a new friend request so add it to the request list
				if (requestStatus.Equals("new"))
				{
					await AppServices.mainPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							AppServices.friendsData.friendRequests.Add(new FriendRequest() { username = sender });
							AppServices.mainPage.DisplayFriendRequestsList();
						});
				}
				// This will be an accepted friend request so add them to the friends list and check if they are online
				else if (requestStatus.Equals("accepted"))
				{
					AppServices.operationComplete = false;
					AppServices.isFriendOnline = false;
					string encryptedName = AppServices.EncryptString(sender, AppServices.secretKey);
					AppServices.sessionService.GetSession(encryptedName, false, new GetFriendSessionCallback(encryptedName));

					while (AppServices.operationComplete == false) ;

					// The friend is online
					if (AppServices.operationSuccessful && AppServices.isFriendOnline)
					{
						await AppServices.mainPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							//AppServices.friendsData.friendsList.Add(new FriendsListEntry() { username = sender, isOnline = true });
							AppServices.mainPage.DisplayFriendsList();
						});
					}
					// The friend is offline
					else
					{
						await AppServices.mainPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							AppServices.friendsData.friendsList.Add(new FriendsListEntry() { username = sender, isOnline = false });
							AppServices.mainPage.DisplayFriendsList();
						});
					}
				}
				// This is a remove request when a friend has removed the local user from their friends list
				else
				{
					await AppServices.mainPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							AppServices.friendsData.friendsList.Remove(AppServices.friendsData.FindFriend(sender));
							AppServices.mainPage.DisplayFriendsList();
						});
				}
			}
			// A friend is informing us that they are establishing a secure session with us so we should generate an encryption key
			else if (updateType.Equals("session"))
			{
				// If I already have a session this is a duplicate
				if (sendingFriend.sessionEstablished)
					return;
				int compareResult = string.Compare(AppServices.localUsernameEncrypted, sendingFriend.encryptedUsername);
				// Use my session ID as the encryption key
				if (compareResult > 0)
				{
					IBuffer keyBuff = CryptographicBuffer.ConvertStringToBinary(AppServices.localSessionId, BinaryStringEncoding.Utf8);
					await AppServices.friendsData._page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							sendingFriend.encryptionKey = AppServices.encryptionAlgorithim.CreateSymmetricKey(keyBuff);
							sendingFriend.sessionEstablished = true;
							sendingFriend.chatButtonsVisible = false;
						});
				}
				// Use friend's session ID as the encryption key
				else
				{
					IBuffer keyBuff = CryptographicBuffer.ConvertStringToBinary(sendingFriend.sessionID, BinaryStringEncoding.Utf8);
					CoreDispatcher dispatcher = AppServices.friendsData._page.Dispatcher;
					await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
						{
							sendingFriend.encryptionKey = AppServices.encryptionAlgorithim.CreateSymmetricKey(keyBuff);
							sendingFriend.sessionEstablished = true;
							sendingFriend.chatButtonsVisible = false;
						});
				}
			}
		}

		public void onRoomCreated(com.shephertz.app42.gaming.multiplayer.client.events.RoomData eventObj)
		{
			throw new NotImplementedException();
		}

		public void onRoomDestroyed(com.shephertz.app42.gaming.multiplayer.client.events.RoomData eventObj)
		{
			throw new NotImplementedException();
		}

		public void onUpdatePeersReceived(com.shephertz.app42.gaming.multiplayer.client.events.UpdateEvent eventObj)
		{
			throw new NotImplementedException();
		}

		public void onUserChangeRoomProperty(com.shephertz.app42.gaming.multiplayer.client.events.RoomData roomData, string sender, Dictionary<string, object> properties, Dictionary<string, string> lockedPropertiesTable)
		{
			throw new NotImplementedException();
		}

		public void onUserJoinedLobby(com.shephertz.app42.gaming.multiplayer.client.events.LobbyData eventObj, string username)
		{
			throw new NotImplementedException();
		}

		public void onUserJoinedRoom(com.shephertz.app42.gaming.multiplayer.client.events.RoomData eventObj, string username)
		{
			throw new NotImplementedException();
		}

		public void onUserLeftLobby(com.shephertz.app42.gaming.multiplayer.client.events.LobbyData eventObj, string username)
		{
			throw new NotImplementedException();
		}

		public void onUserLeftRoom(com.shephertz.app42.gaming.multiplayer.client.events.RoomData eventObj, string username)
		{
			throw new NotImplementedException();
		}

		public void onUserPaused(string locid, bool isLobby, string username)
		{
			throw new NotImplementedException();
		}

		public void onUserResumed(string locid, bool isLobby, string username)
		{
			throw new NotImplementedException();
		}
	}

	public class UpdateListener : com.shephertz.app42.gaming.multiplayer.client.listener.UpdateRequestListener
	{
		public MainPage _page;

		public UpdateListener()
		{

		}

		public UpdateListener(MainPage currPage)
		{
			_page = currPage;
		}

		public async void onSendPrivateUpdateDone(byte result)
		{
			string errorMsg;

			// If the connection was successful remove the progress indicator and return
			if (result == WarpResponseResultCode.SUCCESS)
			{
				AppServices.errorMessage = "Send successful";			
				AppServices.operationSuccessful = true;
				AppServices.appWarpUpdateSent = true;
				return;
			}
			else if (result == WarpResponseResultCode.AUTH_ERROR)
			{
				errorMsg = "UPDATE SEND Authentication Error: Attempting to connect with initializing with API keys.";
			}
			else if (result == WarpResponseResultCode.BAD_REQUEST)
			{
				errorMsg = "Bad Request: The entered username does not exist.";
			}
			else if (result == WarpResponseResultCode.CONNECTION_ERR)
			{
				errorMsg = "Send Update Network Connection Error: Please check your network connection and try again.";
			}
			// Trying to send a message to an offline user so not exactly an error we just need to update the friends list to reflect this but
			//	dont print an error message
			else if (result == WarpResponseResultCode.RESOURCE_NOT_FOUND)
			{
				AppServices.errorMessage = "Update failed (RESOURCE_NOT_FOUND): Friend is not really online.";
				await AppServices.friendsData._page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
					{
						FriendsListEntry friend = AppServices.friendsData.FindFriend(AppServices.appWarpLastSentToUsername);
						friend.isOnline = false;
					});
				AppServices.operationSuccessful = true;
				AppServices.appWarpUpdateSent = true;
				return;
			}
			// Trying to send a message to an offline user so not exactly an error we just need to update the friends list to reflect this but
			//	dont print an error message
			else if (result == WarpResponseResultCode.RESOURCE_MOVED)
			{
				AppServices.errorMessage = "Update failed (RESOURCE_NOT_FOUND): Friend is not really online.";
				await AppServices.friendsData._page.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
				{
					FriendsListEntry friend = AppServices.friendsData.FindFriend(AppServices.appWarpLastSentToUsername);
					friend.isOnline = false;
				});
				AppServices.operationSuccessful = true;
				AppServices.appWarpUpdateSent = true;
				return;
			}
			else
			{
				errorMsg = "Unknown Error: Please check your network connection and the entered username and try again.";
			}

			AppServices.operationSuccessful = false;
			AppServices.errorMessage = errorMsg;
			AppServices.appWarpUpdateSent = true;
		}

		public void onSendUpdateDone(byte result)
		{
			throw new NotImplementedException();
		}
	}
	
	// Helper class to convert between JSON objects and byte data to send over the network. Encryption can be done automatically if a key is supplied
	public class MessageBuilder
	{
		public static JObject decodeMessageBytes(byte[] update, CryptographicKey encryptionKey = null)
		{
			string dataString = System.Text.Encoding.UTF8.GetString(update, 0, update.Length);
			if (encryptionKey == null)
				return JObject.Parse(dataString);
			else
				return JObject.Parse(AppServices.DecryptString(dataString, encryptionKey));
			//return new MessageBuilder(jsonObj["sender"].ToString(), jsonObj["message"].ToString());
		}

		//Converts JSON object into byte array object containing representing C# JSON object
		//  with the sender name and message
		public static byte[] buildMessageBytes(JObject dataToGetBytes, CryptographicKey encryptionKey = null)
		{
			string objectString = dataToGetBytes.ToString();
			if (encryptionKey == null)
				return System.Text.Encoding.UTF8.GetBytes(objectString);
			else
				return System.Text.Encoding.UTF8.GetBytes(AppServices.EncryptString(objectString, encryptionKey));
		}

		public static byte[] buildOnlineStatusUpdateBytes(bool myOnlineStatus, CryptographicKey encryptionKey = null)
		{
			JObject updateObj = new JObject();
			updateObj.Add("updateType", "onlineStatus");
			updateObj.Add("onlineStatus", JsonConvert.SerializeObject(myOnlineStatus));
			updateObj.Add("sessionID", AppServices.localSessionId);
			return MessageBuilder.buildMessageBytes(updateObj, encryptionKey);
		}

		public static byte[] buildNewFriendRequestBytes(CryptographicKey encryptionKey = null)
		{
			JObject updateObj = new JObject();
			updateObj.Add("updateType", "request");
			updateObj.Add("requestStatus", "new");
			return MessageBuilder.buildMessageBytes(updateObj, encryptionKey);
		}

		public static byte[] buildAcceptedFriendRequestBytes(CryptographicKey encryptionKey = null)
		{
			JObject updateObj = new JObject();
			updateObj.Add("updateType", "request");
			updateObj.Add("requestStatus", "accepted");
			return MessageBuilder.buildMessageBytes(updateObj, encryptionKey);
		}

		public static byte[] buildRemoveFriendBytes(CryptographicKey encryptionKey = null)
		{
			JObject updateObj = new JObject();
			updateObj.Add("updateType", "request");
			updateObj.Add("requestStatus", "removed");
			return MessageBuilder.buildMessageBytes(updateObj, encryptionKey);
		}

		public static byte[] buildChatMessageBytes(string messageToSend, CryptographicKey encryptionKey = null)
		{
			JObject updateObj = new JObject();
			updateObj.Add("updateType", "chat");
			updateObj.Add("message", messageToSend);
			byte[] messageBytes = MessageBuilder.buildMessageBytes(updateObj, encryptionKey);
			return messageBytes;//MessageBuilder.buildMessageBytes(updateObj, encryptionKey);
		}

		public static byte[] bulidSessionEstablishedBytes(CryptographicKey encryptionKey = null)
		{
			JObject updateObj = new JObject();
			updateObj.Add("updateType", "session");
			
			return MessageBuilder.buildMessageBytes(updateObj, encryptionKey);
		}
	}

	// Callback class for saving IP data in the cloud database
	public class SaveIPCallback : App42Callback
	{
		public void OnException(App42Exception exception)
		{
			throw new NotImplementedException();
		}

		public void OnSuccess(object response)
		{
			throw new NotImplementedException();
		}
	}
}
