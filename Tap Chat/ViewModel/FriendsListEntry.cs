using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace Tap_Chat.ViewModel
{
	public class FriendsListEntry : INotifyPropertyChanged
	{
		public FriendsListEntry()
		{
			history = new ObservableCollection<ChatEntry>();
		}

		private string _username;
		public string username
		{
			get
			{
				return _username;
			}
			set
			{
				// When a friend is retrieved from the cloud, we will retrieve the encrypted username, 
				//	so we must decrypt it first before we save it
				_username = value;
				_encryptedUsername = AppServices.EncryptString(_username, AppServices.secretKey);

				RaisePropertyChanged("username");
			}
		}

		private string _encryptedUsername;
		public string encryptedUsername 
		{
			get 
			{
				return _encryptedUsername;
			}
			set
			{
				_encryptedUsername = value;
				_username = AppServices.DecryptString(_encryptedUsername, AppServices.secretKey);
				RaisePropertyChanged("username");
			}
		}

		private string _sessionID = string.Empty;
		public string sessionID
		{ 
			get 
			{
				return _sessionID;
			} 
			set
			{
				_sessionID = value;
			}
		}

		private CryptographicKey _encryptionKey = null;
		public CryptographicKey encryptionKey 
		{ 
			get
			{
				return _encryptionKey;
			}	
			set
			{
				_encryptionKey = value;
				if (_encryptionKey != null)
					this.sessionEstablished = true;
				else
					this.sessionEstablished = false;
			}
		}

		public bool sessionEstablished = false;

		private bool _isOnline;
		public bool isOnline
		{
			get
			{
				return _isOnline;
			}
			set
			{
				_isOnline = value;
				// Change the online status indicator color depending on if the friend is online
				if (_isOnline)
				{
					this.onlineStatusColor = _greenColor;
				}
				else
				{
					this.onlineStatusColor = _redColor;
				}

				// If someone is signed off we cannot have a session or show the chat buttons
				if (_isOnline == false)
				{
					this.encryptionKey = null;
					this.chatButtonsVisible = false;
				}
				RaisePropertyChanged("isOnline");
			}
		}

		private static SolidColorBrush _greenColor = new SolidColorBrush(Colors.Green);
		private static SolidColorBrush _redColor = new SolidColorBrush(Colors.Red);

		private SolidColorBrush _onlineStatusColor;
		public SolidColorBrush onlineStatusColor
		{
			get
			{
				return _onlineStatusColor;
			}
			set
			{
				_onlineStatusColor = value;
				RaisePropertyChanged("onlineStatusColor");
			}
		}

		// Integer which controls how many unread messages are displayed on the friends list
		private int _unreadCount = 0;
		public string unreadCount
		{
			get
			{
				return _unreadCount.ToString();
			}
			set
			{
				_unreadCount = Convert.ToInt32(value);
				if (_unreadCount > 0)
				{
					unreadCountVisibility = Windows.UI.Xaml.Visibility.Visible;
				}
				else
				{
					unreadCountVisibility = Windows.UI.Xaml.Visibility.Collapsed;
				}
				RaisePropertyChanged("unreadCount");
			}
		}

		private Windows.UI.Xaml.Visibility _unreadCountVisibility = Windows.UI.Xaml.Visibility.Collapsed;
		public Windows.UI.Xaml.Visibility unreadCountVisibility
		{
			get
			{
				return _unreadCountVisibility;
			}
			set
			{
				_unreadCountVisibility = value;
				RaisePropertyChanged("unreadCountVisibility");
			}
		}

		// Boolean which controls if the chat buttons are visible. When it is set it changes the visibility property
		//	which the buttons on the friends list page are bound to.thus they will change their visibility
		private bool _chatButtonsVisible = false;
		public bool chatButtonsVisible
		{
			get
			{
				return _chatButtonsVisible;
			}
			set
			{
				// When someone tries to change the chat button visibility only change it if the user is online
				if (this.isOnline)
					_chatButtonsVisible = value;
				else
					_chatButtonsVisible = false;
				// Chat buttons should only show if we don't already have a session
				if (_chatButtonsVisible && this.sessionEstablished == false)
				{
					showChatButtons = Windows.UI.Xaml.Visibility.Visible;
				}
				else
				{
					showChatButtons = Windows.UI.Xaml.Visibility.Collapsed;
				}
			}
		}

		private Windows.UI.Xaml.Visibility _showChatButtons = Windows.UI.Xaml.Visibility.Collapsed;
		public Windows.UI.Xaml.Visibility showChatButtons
		{
			get
			{
				return _showChatButtons;
			}
			set
			{
				_showChatButtons = value;
				RaisePropertyChanged("showChatButtons");
			}
		}

		// Stores chat history with this friend
		public ObservableCollection<ChatEntry> history { get; set; }

		// My encryption key for sending messages
		public string myKey { get; set; }

		// Friends encryption key for decoding messages
		public string friendsKey { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		private void RaisePropertyChanged(string propertyName)
		{
			if (this.PropertyChanged != null)
			{
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}

	// This class represents a single message in the chat history.
	public class ChatEntry : INotifyPropertyChanged
	{
		private string _sender;
		public string sender 
		{
			get
			{
				return _sender;
			}
			set
			{
				_sender = value;
				RaisePropertyChanged("sender");
			}
		}
		private string _message;
		public string message 
		{ 
			get
			{
				return _message;
			}
			set
			{
				_message = value;
				RaisePropertyChanged("message");
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
