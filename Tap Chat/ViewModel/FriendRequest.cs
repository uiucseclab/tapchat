using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tap_Chat.ViewModel
{
	public class FriendRequest : INotifyPropertyChanged
	{
		private string _username;
		public string username
		{
			get
			{
				return _username;
			}
			set
			{
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

		private Windows.UI.Xaml.Visibility _showRequestButtons = Windows.UI.Xaml.Visibility.Collapsed;
		public Windows.UI.Xaml.Visibility showRequestButtons 
		{
			get
			{
				return _showRequestButtons;
			}
			set
			{
				_showRequestButtons = value;
				RaisePropertyChanged("showRequestButtons");
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
