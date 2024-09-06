# BusinessAsUsual

This mod makes the sales ledgers in Shadows of Doubt more dynamic, generating sales for NPCs across all businesses, making it harder to pick a murderer out of the ledgers (ie in the Weapons Dealer where only murderers make a purchase)

## What it does
- Sales will now be generated for NPCs for all businesses that have a sales ledger, rather than relying on NPCs to buy stuff themselves (eg gun shops who only ever get visited by the murderer)
- NPCs will get the item they "purchased". Hopefully over time this will mean less visits from murderers to stores because they will already have the items they need.
- Ledger data is only kept for 24 hours. This brings it in line with other evidence in the game (such as CCTV and fingerprints) - and also stops the memory footprint from getting too high due to all the extra entries we are generating.

## How it works
- If a business is already naturally generating high amounts of sales (ie diners etc that NPCs frequent naturally) we'll leave that alone and not generate any sales.
- Otherwise, every time an NPC sets foot in the store, we roll a dice based on how many sales entry that business already has (low sales = more likely to generate a purchase, high sales = less likely). If it passes, the NPC gets that item and an entry in the sales ledger. This way if you monitor CCTV etc, you can see that NPC was in the store at the right time.
- If a business for whatever reason does not generate enough sales or foot traffic naturally, we will fall back on creating a few fake entries for random citizens in the city. Those citizens will still get the item they "purchased" they just won't have actually been in the store.
- If you don't want NPCs buying certain items (like say ballistic armour) you can add that item to the blacklist.txt file and we won't generate sales for that item. This is currently used to stop citizens buying briefcase bombs because it looks odd seeing waiters wandering around carrying a briefcase. This uses "fuzzy matching" so you can just put "briefcase" for example and it will blacklist anything with "briefcase" in the name.
