# rate-your-genres
A musicbee plugin for automatically pulling album genres from rateyourmusic.com. I made this for a friend, but I figured I may as well open source it.

Place the centents of the release in your plugins folder, and enable. Select any music you want to search for genres, then go under Tools>Find genres for selected tracks.
A log of any albums it did not find is placed under %APPDATA%/Roaming/MusicBee/rateyourgenres. Please, report any music it should have found but did not to me. It may have trouble with unconventional symbols.

**Warning**: Extensive use in a short period of time may cause the website to block you for a little while. However, I hammered it with 10,000 requests over about 10 minutes, and it didn't trip the DDOS protection, so this shouldn't be an issue. 
If you're curious, it will make at maximum 6 requests per album.

It will only apply the genres to the selected songs, so make sure you've got the whole album highlighted.
