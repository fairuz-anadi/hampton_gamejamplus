/// <summary>
/// Everything the Captain says outside the 12 order lines (those live in the OrderData assets). Owner: Riham.
///
/// Who he is: a captain who seems to want a fast crossing and the credit for it, and who works on the crew to get both.
/// He starts warm and trustworthy and seems to drink as the night goes on; the drinking shows in how he talks far more
/// than in what he says. It is an act. The owners insured the ship for three times her worth and pay him a share to
/// lose her off Grey Cove, where wreckers wait to row him and the Stoker away. A drunk captain who ignored the ice
/// warnings is the story the inquiry will believe. He only says so in the final speech.
/// Mood (0-4) is picked by CaptainDirector:
///   0  Part 1          warm, sarcastic, trustworthy
///   1  orders 6-7      looser, pushier
///   2  orders 8-9      tipsy
///   3  orders 10-11    drunk
///   4  Part 3          quiet, serious
/// Tiered lines are arrays indexed by mood; the game picks one line at random from the right mood.
///
/// Serious on purpose: caught fakes, the final speech, the ice warnings and the endings.
/// Don't over-explain. Let the atmosphere carry it.
///
/// Formatting:
///   {seat} = the crew member's name, e.g. "Ezra", {task} = the order's results line, e.g. "Feed boiler two".
///   \n starts a new text box (page). Keep the first page self-contained: when the speaker is busy, later pages give way.
///   *hic*, *clink*: stage directions, typed like any other text. Use sparingly.
/// </summary>
public static class CaptainScript
{
    public const int Moods = 5;

    // ---------- Part changes ----------

    public const string Part1Start =
        "Ahem. Is this on? It's on.\n" +
        "Good morning, crew! This is your Captain speaking. Well, typing. Well, speaking. You'll get used to me.\n" +
        "Two days to the Promised Port. Clear skies, calm sea, and a crew that does exactly what it's told. I can already tell we'll get along.";

    public const string Part2Start =
        "Evening, crew! Sea like glass. Stars out. I've loosened my collar, which is the most exciting thing that's happened all day.\n" +
        "We're ahead of schedule. Let's be more ahead of it.";

    /// <summary>Said once, right before the first order of each new mood (2 and 3).</summary>
    public static readonly string[] MoodStart =
    {
        "",
        "",
        "Quick announcement: the bridge is perfectly level. If it feels tilted, that's a you problem.\n" +
        "Right. Where were we. Orders. Yes.",
        "Right. RIGHT. Everyone listen. I have been very patient. Very. Patient.\n" +
        "I've been patient for hours. Days. It's been a long evening and I've been patient in all of it.",
        "",
    };

    public const string IcebergCall = "That bell means something. I've forgotten what.\nHands on the wheel, crew.";

    // ---------- Reports ----------

    /// <summary>
    /// Every report sounds the same on the speaker, honest or fake, and it's one text box. That's how players can
    /// catch a false report: the line is identical, only where the crew member was standing differs.
    /// </summary>
    public const string ReportDone = "{seat} says done. Done is my favourite word.";

    /// <summary>Sometimes added after a report, as its own text box.</summary>
    public static readonly string[][] Praise =
    {
        new[]
        {
            "Good work! We'll make history, you and I.",
            "Lovely. Steady hands make a steady ship. I'd shake yours, but I'm up here and you're down there. Consider it shaken.",
            "That's the spirit! I knew I picked a good crew. Well, the company picked you. But I approved. In spirit.",
        },
        new[]
        {
            "Good. Very good. Speed and obedience, my two favourite things.",
            "Lovely. Keep that up and you'll be captain one day. Of something. Smaller.",
        },
        new[]
        {
            "Oh, look at that. Someone on this ship can follow an order. Gold star. *hic*",
            "Finally. Was that so hard? Don't answer. It was a rhetorical. A rhetoric. A question.",
        },
        new[]
        {
            "Thash... that's more like it. You're my favourite now. Everyone else is fired.",
            "Good. GOOD. See? When you do what I say, nothing bad happens. Usually. Mostly. Shh.",
        },
        new[] { "Good." },
    };

    // ---------- Refusals (public, everyone hears) ----------

    public static readonly string[][] Refuse =
    {
        new[]
        {
            "Refused? Oh. Well, I'm sure you have your reasons, {seat}.\nI'll just make a note. A small one. In pencil.",
            "{seat} would rather not. That's all right! It's a free ship.\nMostly free. Free-ish.",
        },
        new[]
        {
            "Refused. Interesting.\n{seat}, I'm going to pretend I didn't hear that, and you're going to pretend you didn't say it.",
            "That's alright, {seat}. Someone else will do it.\nSomeone better.",
        },
        new[]
        {
            "{seat} decided not to do as they were told. {seat} believed they knew better than the Captain.\n{seat} was wrong. *hic*",
            "No? NO? I give you one simple job, {seat}. One.\nI've had harder jobs opening this bottle.",
        },
        new[]
        {
            "Oh, you're REFUSING? You, {seat}? You couldn't steer a bathtub. I've met barnacles with more initiative.\nWhen we dock, I'm having your face painted on the hull. As a warning.",
            "Refuse me one more time, {seat}. Go on. I dare you.\nI'll have you scrubbing the anchor. Underwater. *hic*",
        },
        new[]
        {
            "Refused. It doesn't matter anymore, {seat}.",
            "Go on, refuse. The sea doesn't take orders either.",
        },
    };

    // ---------- Timer ran out ----------

    public static readonly string[][] Expired =
    {
        new[]
        {
            "Too slow, {seat}. Never mind! Plenty of voyage left.",
            "{seat}? Hello? Is this thing on?\nAh well. We'll call that a practice run.",
        },
        new[]
        {
            "Hesitating, {seat}? Hesitation is how ships get late.",
            "Silence is an answer too, {seat}. Not a good one.",
        },
        new[]
        {
            "{seat} stood very still and did nothing at all.\nFascinating choice. Truly. They'll write songs about it.",
            "I said NOW, {seat}. Not 'whenever you fancy'.\nIs there an echo down there? Echo? ...No? Fine.",
        },
        new[]
        {
            "Asleep, {seat}? Dead? Either way, I'm docking your pay.",
            "Hello? {seat}? Did you die?\nIf you died, you're excused. Otherwise, very rude.",
        },
        new[] { "It doesn't matter now." },
    };

    // ---------- He caught a fake (serious at every mood) ----------

    /// <summary>The Captain "noticed" a fake. There is no camera in that room.</summary>
    public static readonly string[] FakeNoticed =
    {
        "\"{task}.\" You said it was done, {seat}. It wasn't.\nThere was no one in that room but you. And yet, here we are.",
        "{seat}. Your report on \"{task}\". I checked.\nI always check. I'd like you to remember that.",
        "No one saw you skip \"{task}\", {seat}. No one except me.\nI see a great deal from up here. More than you'd think.",
    };

    /// <summary>Order 8 (reopen door four): he knows how order 3 went. Added as its own text box.</summary>
    public const string DoorFourWasClosedBy = "Thank {seat} for closing it earlier. I saw.";
    /// <summary>Same as above when the one reopening it is the one who closed it.</summary>
    public const string DoorFourYouClosed = "You again. I'm sure the door remembers you fondly.";
    public const string DoorFourWasNeverClosed = "It's already open, isn't it, {seat}.\nFunny how doors do that.";
    public const string DoorFourWasRefused = "Someone wouldn't close it the first time. I know who. I haven't forgotten.";

    // ---------- Asides in Part 2, when the speaker has been quiet ----------

    /// <summary>Captain's Trust 75+: he thinks you're on his side and tells you things.</summary>
    public static readonly string[] PrivateLines =
    {
        "Between us, {seat}: the ice reports are always exaggerated. The wireless lads love a drama.\nStick with me and you'll have a medal by Thursday.",
        "{seat}, you're my favourite. Don't tell the others.\nThe others hesitate. You don't. I like that in a crew member. And in a dog.",
        "Off the record, {seat}. The lifeboats are for show. Like the band.\nWe won't be needing them.",
        "Between you and me, {seat}, there's a fellow from the papers waiting at the port.\nI've been practising my modest face. You can't see it. It's very modest.\n" +
        "When they ask who made this crossing possible, I'm going to say \"the crew\".\nAnd then I'm going to say \"just kidding\".",
        "Shall I tell you a secret, {seat}? This rum was a gift from the owners. For breaking the record.\nI'm just celebrating a little early.",
    };

    /// <summary>Captain's Trust 25 or less: aimed at whoever refused him most.</summary>
    public static readonly string[] SuspiciousLines =
    {
        "I'm watching you, {seat}.\nNot with my eyes. I have other ways.",
        "Some of you have forgotten whose ship this is. {seat} certainly has.\nLet me remind you. Mine. The hat says so.",
        "{seat}. Every refusal goes in my log.\nThe log is getting very long. I'm going to need a second log.",
    };

    /// <summary>Everything else: the Captain rambling into the speaker, by mood (1-3 are used).</summary>
    public static readonly string[][] Rambles =
    {
        new string[0],
        new[]
        {
            "Fun fact: a ship this size takes over a mile to stop.\nSo it's better not to.",
            "I once captained a ship so fast we arrived the day before we left.\nNobody believes that story. That's fine. I was there.",
        },
        new[]
        {
            "You know the problem with icebergs? Nobody ever sees the bottom of them.\nSame with people. Same with this bottle. Where has the bottom of this bottle gone?",
            "Note to the owners. Crew morale: excellent. Crew obedience: questionable.\nCaptain: magnificent. Rum: running low.",
        },
        new[]
        {
            "Who put the North Pole there? Was it the owners? I bet it was the owners.\nSomebody write them a letter. A stern one. In CAPITALS.",
            "I want you all to know that if anything goes wrong tonight, legally, it's your fault.\nI checked. I checked with myself. *hic*",
            "I was going to be an admiral, you know. Then a duke. Now I'm going to be a record. A NAME.\nNobody remembers a careful man! ...What was I saying. Shovel something.",
        },
        new string[0],
    };

    // ---------- Ice warnings delivered to the bridge ----------

    public static readonly string[][] IceWarningDelivered =
    {
        new[] { "Another ice message on my desk. Thank you. Very thorough.\nI'll read it right after this. Other thing." },
        new[] { "Yes, yes. Ice. There's always ice.\nIt's the North Atlantic, not a punch bowl." },
        new[] { "Oh, MORE ice? Lovely.\nPut some in my drink." },
        new[] { "Who keeps bringing me these? Is it you, {seat}?\nStop it. Stop being helpful. It's very annoying." },
        new string[0],
    };

    // ---------- Muster vote (Part 2-3, so he's had a few) ----------

    public static readonly string[] VoteCalled =
    {
        "{seat} is ringing the muster bell. How dramatic.\nShall I fetch you a little gavel?",
        "A vote? On MY ship? Democracy. Disgusting.\nGo on then, {seat}. Quickly.",
    };
    public const string VoteNobody = "Nobody? Nobody at all?\nThen back to work, the lot of you. That was a waste of everyone's evening.";
    /// <summary>They locked out an honest crewmate.</summary>
    public static readonly string[] VoteWrong =
    {
        "You've locked out {seat}. Wonderful. One fewer pair of hands, and one more lesson in trust.\nI couldn't have planned it better. ...I didn't plan it. Probably.",
        "{seat}, off the stations. Your crewmates don't trust you. Neither do I.\nNobody does. Go and stand in the corner.",
    };
    /// <summary>They caught the Stoker.</summary>
    public static readonly string[] VoteRight =
    {
        "Hm. {seat}. Fine.\nBack to work. Nothing to see here. Nothing at all.",
        "{seat}? An odd choice. A very odd choice.\nNever mind. Carry on. I said carry ON.",
    };

    // ---------- Onboarding: the Captain teaches by reacting (first voyage only, each line once) ----------
    // {station} = where the order is, e.g. "the Boiler".

    /// <summary>Has an order, standing still away from the station.</summary>
    public const string TutorialIdle = "{seat}? You're standing very still for someone with a job.";
    /// <summary>Carry order, at the pick-up spot, not holding the item yet.</summary>
    public const string TutorialPickUp = "It's the paper, {seat}. On the desk. You pick it up.\nI believe in you. Moderately.";
    /// <summary>Just picked up a carry item.</summary>
    public const string TutorialCarrying = "Lovely, you've got it. Now the other end of the ship.\nThe pointy end.";
    /// <summary>At the station but not working it.</summary>
    public const string TutorialAtStation = "Go on, {seat}. It doesn't bite.\nThe boiler bites. The rest don't.";
    /// <summary>Working a two-person station alone.</summary>
    public const string TutorialAlone = "{seat}, that's a two-person job, and you are, as far as I can tell, one person.\nGive a shout. Someone will come. Eventually.";
    /// <summary>Station is green, not reported yet.</summary>
    public const string TutorialGreen = "It's green, {seat}. Green means done. Done means you tell me.\nI can't hear lights.";
    /// <summary>Right after the first off order a human gets.</summary>
    public const string TutorialOffOrder = "If anyone has a problem with that, keep it to yourself.\nLoud refusals help no one.";

    // ---------- Wireless: the ice warnings (serious) ----------

    /// <summary>The text on each ice warning (MSG 1-3). MSG 1 and 3 are easter eggs after real April 1912 ice messages.</summary>
    public static readonly string[] IceWarningText =
    {
        "MSG 1. Westbound steamers report bergs, growlers and field ice in 42 N, from 49 to 51 W.",
        "MSG 2. Large icebergs and field ice directly in your track. Advise reduce speed.",
        "MSG 3. Saw much heavy pack ice and great number large icebergs, also field ice. Weather good, clear.",
    };

    // ---------- Part 3: the final speech (serious) ----------
    // The confession. Two versions, the same up to his way out. CaptainDirector picks one when the last order is done,
    // from how the voyage has gone: MonologueLost when it's going badly for the crew (Ship Safety under 35, or the
    // lifeboat store still locked), so his plan is working and he gloats; Monologue otherwise, and he's irritated.
    // The iceberg can still change the ending either way.

    /// <summary>The line that turns the HUD's port readout to Grey Cove (CaptainDirector calls ShipReadout.RevealGreyCove).</summary>
    public const string RevealLine =
        "We were never going to the Promised Port.\nCheck the readout. Grey Cove. Lovely spot. Nobody lives there.";

    /// <summary>The voyage is going well for the crew: the plan is in trouble, and he's irritated.</summary>
    public static readonly string[] Monologue =
    {
        "Crew. All of you. Listen.\nThis is it. This is the bit.\nThe bottle's empty.",
        "Funny thing about this bottle. It was never full.",
        RevealLine,
        "She's insured for three times what she's worth.\nThe owners get paid when she sinks, and I get my share.",
        "And when they ask why, the answer's easy.\nThe Captain was drunk. Everyone heard him.",
        "The Stoker's been on my payroll since we left port.\nWave, Stoker.",
        "The wreckers in the cove will row me home.\nYou, I'm afraid, they won't.",
        "You questioned. You dragged your feet. You read things you weren't meant to read.",
        "Too late now. She's pointed at the ice and she's going fast.",
        "...",
        "Lookout's ringing. Your hands are on the wheel now. Not mine.",
    };

    /// <summary>The voyage is going badly for the crew: the plan is working, and he gloats.</summary>
    public static readonly string[] MonologueLost =
    {
        "Crew. All of you. Listen.\nThis is it. This is the bit.\nThe bottle's empty.",
        "Funny thing about this bottle. It was never full.",
        RevealLine,
        "She's insured for three times what she's worth.\nThe owners get paid when she sinks, and I get my share.",
        "And when they ask why, the answer's easy.\nThe Captain was drunk. Everyone heard him.",
        "The Stoker's been on my payroll since we left port.\nWave, Stoker.",
        "The wreckers in the cove will row me home.\nYou, I'm afraid, they won't.",
        "Boats or no boats, nobody rows away from ice this fast.\nI couldn't have asked for a better crew.",
        "Every order, you followed. Thank you. Really.",
        "...",
        "Lookout's ringing. Your hands are on the wheel now. Not mine.",
    };

    /// <summary>True when the speech should be the disappointed one: same test as the "Many saved" ending, before the impact.</summary>
    public static bool VoyageGoingBadly(int shipSafety, bool lifeboatsLocked) =>
        shipSafety < EndingEvaluator.ManySavedMinSafety || lifeboatsLocked;

    // ---------- The Captain's last word, on the results screen ----------

    public const string EpilogueShipSaved = "So. About that speech.\nI was drunk. You all heard me. Very drunk.";
    public const string EpilogueManySaved = "Well. She's gone. But look at all those boats.\nI always said this was a lifeboat sort of ship.";
    public const string EpilogueShipLost = "I gave the orders. You're the ones who followed them.\nRemember that, when they ask.\nShe's never been worth more.";

    /// <summary>His last word for an ending. Anadi / Samprity: show this on the results screen.</summary>
    public static string EpilogueFor(Ending ending) => ending switch
    {
        Ending.WhatIf => EpilogueShipSaved,
        Ending.ManySaved => EpilogueManySaved,
        _ => EpilogueShipLost,
    };

    /// <summary>The line list for a mood, clamped to 0..4. Empty arrays mean "say nothing" at that mood.</summary>
    public static string[] ForMood(string[][] tiers, int mood) => tiers[System.Math.Clamp(mood, 0, tiers.Length - 1)];
}
