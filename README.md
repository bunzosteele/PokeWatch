# PokeWatch
A twitter bot that uses the Pokemon Go API to find rare pokemon.

Bellingham, WA PokeWatch: https://twitter.com/BhamPokeWatch

Other known bots running Pokewatch: https://twitter.com/BhamPokeWatch/lists/pokewatchbots

Subreddit: https://reddit.com/r/pokewatch

DOWNLOAD: https://db.tt/wNDXXmgr

##Set Up Instructions
1. GETTING THE FILES:

	a. Download and Pokewatch.zip and extract files (https://db.tt/wNDXXmgr).
	
	b. Alternatively, clone and build the repo.
	
2. CREATING POKEMONGO ACCOUNTS:

	Only one user account is needed for each bot, but creating both a PTC account
	and a google account will allow the bot to function if either sign in system goes offline.
	YOU SHOULD NOT USE YOUR PERSONAL ACCOUNT
	
	a. Create a Gmail account for the bot (https://accounts.google.com/signup)
	
	b. Create a PTC account for the bot (https://club.pokemon.com/us/pokemon-trainer-club/sign-up/)
	
	c. You will need the username and password for both acccounts later.
	
3. CREATING TWITTER ACCOUNT:

	If you want to tweet from a personal/existing twitter account, skip part a.
	
	a. Create a twitter account for the bot (https://twitter.com/signup)
	
	b. Create a new twitter application while signed in as the bot's account (https://apps.twitter.com/app/new)
	
	c. You will need the Consumer Key, Consumer Secret, Access Token, and Accesss Secret from the application's
	   management page, under the Keys and Access Tokens tab
	   
4. SETTING UP THE BOT:

	a. Open Configuration.json
	
	b. Set PTCUsername and PTCPassword to your PTC account credentials if you created one.
	
	c. Set GAUsername and GAPassword to your Google account credentials if you created one.
	
	d. Set TwitterConsumerToken to your twitter app's Consumer Key, TwitterConsumerSecret to Consumer Secret, TwitterAccessToken to Access Token and TwitterAccessSecret to Access Secret
	   
5. CUSTOMIZE BOT FUNCTIONALITY:

	You can change the following fields to customize the functionality of your app:
	
	MinimumLifeExpectancy: Minimum ammount of time that a pokemon can have before despawning that you still want
		to tweet about, in seconds. (so you only tweet about pokemon that will be around long enough to catch)
		
	RateLimit: Minimum ammount of time in seconds between tweets (to prevent spam)
	
	ExcludedPokemon: Pokedex ids for common pokemon that you want to ignore.
	
	PriorityPokemon: Pokedex ids for extremely rare pokemon that will be tweeted with an emphasis and will ignore any rate limits.
	
	TagPokemon: [true or false] Should a hashtag for the found pokemon be included.
	
	TagRegion: [true or false] Should a hashtag for the region be included.
	
	CustomTags: A list of extra hashtags to include.
	
	RegularTweet: A recipe for the tweet to send out when a pokemon is found. If you are unfamiliar with string formatting,
	be careful, messing up the syntax will cause your bot to crash.
	
		{0}: Name of pokemon found
		
		{1}: Prefix for region where pokemon was found.
		
		{2}: Name of region where pokemon was found.
		
		{3}: Suffix for region where pokemon was found.
		
		{4}: Time that found pokemon will expire.
		
		{5}: Link to google maps
		
	PriorityTweet: Same as regular tweet, but will be used when a pokemon included in PriorityPokemon is found.
	
	Regions: Areas to be searched by the bot. Each region consists of a name, prefix, suffix, and a list of locations.
	
		Name: Human friendly display name that describes the general area of the listed locations
		
		Prefix: Text to make the Name make sense in the context of a sentence
		
		Suffix: Same as prefix, but after the Name
		
		Locations: List of GPS coordinates that the bot will scan for pokemon in. The bot will identify pokemon within
			~200 meters of that point. Double clicking on google maps will give you the GPS coordinates.
			
6. ENABLE REBOOTING

	PokewatchLauncher.exe will restart pokewatch any time it dies in the middle of running (usually due to server outages). By default on most machines, OS will prompt you to acknowlege the crash before PokewatchLauncher can reset the application, to disable this and allow the app to run indefinitely without supervision, follow this guide: https://www.raymond.cc/blog/disable-program-has-stopped-working-error-dialog-in-windows-server-2008/
           
7. RUNNING THE APP

	Run PokewatchLauncher.exe
	
8. OPTIONAL:

	a. Due to limitations with the PokemonGo API, any given PokemonGo user can only update their location every 4 seconds,
	   therefore, it is not recommended to include more than 50 Locations to scan on a single bot.
	   Each location has a radius of 200m.
	   With more locations, it will on average take longer to find pokemon after they spawn.
	   The faster they are found and tweeted, the more time people have to find and catch them.
	   If you want to scan more locations, you should repeat this process for a second bot, with different GPS locations, but reuse the credentials from Step 3.
           
##Credits
https://github.com/AeonLucid/POGOLib


