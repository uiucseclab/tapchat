Tap Chat
=================
####Secure Messaging for Windows Phone 8.1

CS 460 Spring 2014  
Feraas Khatib - khatib3  
Gustavo Frankowiak - frankow1  
  
**Features:**  

* AES Encryption
* NFC Functionality
* Full user system with friend management
* Easily add friends by tapping phones together and get a random generated 256 character encryption key for a secure chat session
* All data is stored fully encrypted in a remote database
* All user data (usernames, passwords, friend requests etc..) is sent fully encrypted and over SSL
* No data is ever sent plaintext over the internet
* Each friend has a different encryption key that is used for that session
* Start a secure chat session using the internet and get a unique key **or** for more security just tap phones together and get a random generated key that is never sent over the internet  
  
**Future Enhancements:**  
  
* Clean up and debug code. Everything works fine if used exactly as intended but there is very little handling for user error.
* Enable saving sessions and running in the background so that users can tap and generate a key and save that key for a long time
* Add secure message storage so that messages are not deleted after the app is closed
* Add SSL protection for messages not only user data
* Increase security for sessions established without using NFC