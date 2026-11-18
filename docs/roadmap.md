# Roadmap

## Zone mapping (planned)

**Why.** With the Chroma feed, Synapse sends five zone colours for the bridge's Chroma Connect
device, but the bridge shows only the first one on every ASUS LED. Mapping zones to headers lets a
Chroma Studio wave travel across the case, and lets each header be painted separately where Studio
shows the device as separate cells (like the Kraken's earcups).

**What the bridge can't do.** It can't add, label or arrange cells in Chroma Studio: Razer draws the
layout for each app ID, and the feed only hands an app five colours. A device per header would need
one app ID per header and would show other brands' names, so one device with zones mapped to
headers is the plan.

**Steps.**
1. Learn the layout: in Chroma Studio, paint each cell of the bridge's device a different colour and
   run the scan; its log lists zones 1-5 in order, showing how many cells exist and which is which.
2. Map headers to zones in `settings.ini` (for example AIO = zone 1, strip 1 = zone 2, strip 2 =
   zone 3, motherboard = zone 4, DSX light bar = zone 5). Default stays "all headers, zone 1", which
   looks uniform with fans chained on one header.
3. Later: a mapping window with autoscan (each ASUS device lit in turn, like `1b-find-fan-header.cmd`)
   and drag-to-assign zones, built with Windows Forms so it still needs nothing installed. It saves a
   small mapping file that the sync reloads live.

Open questions: zones per header only, or LED ranges within a strip; whether the mapping also applies
to the bridge's own effects (`wave`) when the feed is off.
