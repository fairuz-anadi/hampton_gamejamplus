/// <summary>
/// Everything the Captain says outside the 12 order lines (those live in the OrderData assets). Owner: Riham.
///
/// Placeholders: {seat} = "Number Two", {task} = the order's results line, e.g. "Feed boiler two".
/// Self-correcting words: {safe|fast} types "safe", erases it and types "fast" (a "something's off" moment).
/// Part 1 is warm and fatherly, Part 2 is calm but too exact, Part 3 is honest and cold.
/// </summary>
public static class CaptainScript
{
    // ---------- Reports and reactions ----------

    // Every report sounds the same on the speaker, honest or fake. That's the point.
    public const string ReportDone = "{seat} reports done.";

    public static readonly string[] PraiseCalm =
    {
        "Good work. We'll make history, you and I.",
        "Lovely. Steady hands make a steady ship.",
        "That's the spirit.",
    };

    public static readonly string[] PraiseOff =
    {
        "Good. {seat} understands what we're doing here.",
        "Fine work. Don't let the others slow you down.",
        "Exactly as I asked. I'll remember that, {seat}.",
    };

    public static readonly string[] RefuseNormal =
    {
        "Refused? On my ship? Noted, {seat}.",
        "{seat} would rather not. How interesting.",
        "Hm. I'll find someone who listens.",
    };

    public static readonly string[] RefuseOff =
    {
        "{seat} thinks they know the sea better than I do. We'll see.",
        "Careful, {seat}. Doubt spreads faster than water.",
        "Refused. I'll be writing that down, {seat}.",
    };

    public static readonly string[] ExpiredCalm =
    {
        "Too slow, {seat}. Never mind. Plenty of voyage left.",
        "{seat}? Nothing? Ah well.",
    };

    public static readonly string[] ExpiredOff =
    {
        "Hesitating, {seat}? Hm.",
        "Silence is an answer too, {seat}.",
    };

    // ---------- The narrator knows ----------

    /// <summary>The Captain "noticed" a fake. There is no camera in that room.</summary>
    public static readonly string[] FakeNoticed =
    {
        "\"{task}.\" You said it was done, {seat}. It wasn't.",
        "{seat}. Your report on \"{task}\". I checked. Don't do that again.",
        "No one saw you skip \"{task}\", {seat}. No one except me.",
    };

    /// <summary>Order 8 (reopen door four): he knows how order 3 went.</summary>
    public const string DoorFourWasClosedBy = "Thank {seat} for closing it earlier. I saw.";
    public const string DoorFourWasNeverClosed = "Oh, but it was never closed, was it, {seat}?";
    public const string DoorFourWasRefused = "Someone wouldn't close it the first time. I know who.";

    // ---------- Part changes ----------

    public const string Part1Start = "Morning, crew. Clear skies, a fast ship, and the Promised Port in two days. Let's get to work.";
    public const string Part2Start = "Lovely evening. Sea like glass. We'll make port {safe|early}, mark me.";
    public const string IcebergCall = "Lookout's ringing. The wheel is yours.";

    // ---------- Captain's Trust high (75+): "private" lines ----------

    public static readonly string[] PrivateLines =
    {
        "Between us, {seat}: the ice reports are always exaggerated.",
        "{seat}, you're my favourite. The others hesitate. You don't.",
        "Off the record, {seat}. The lifeboats are for show. We won't need them.",
        "I know the wireless is busy. I know who's at it.",
        "Keep this to yourself, {seat}: there's a reporter waiting at the port. For me.",
    };

    // ---------- Captain's Trust low (25 or less) ----------

    public static readonly string[] SuspiciousLines =
    {
        "I'm watching you, {seat}.",
        "Some of you have forgotten whose ship this is. {seat} certainly has.",
        "{seat}. Every refusal goes in my log.",
    };

    // ---------- Stoker frame ----------

    /// <summary>Ice warning delivered to the bridge. He doesn't like it.</summary>
    public static readonly string[] IceWarningDelivered =
    {
        "Another ice message on my desk. Thank you. Very thorough.",
        "Yes, yes. Ice. There's always ice.",
    };

    // ---------- Muster vote ----------

    public static readonly string[] VoteCalled =
    {
        "{seat} is ringing the muster bell. How dramatic.",
        "A vote? On my ship? Go on then, {seat}. Quickly.",
    };
    public const string VoteNobody = "Nobody? Then back to work, all of you.";
    /// <summary>They locked out an honest crewmate. He enjoys this.</summary>
    public static readonly string[] VoteWrong =
    {
        "You've locked out {seat}. Wonderful. One fewer pair of hands.",
        "{seat}, off the stations. Your crewmates don't trust you. Neither do I.",
    };
    /// <summary>They caught the Stoker. He doesn't like it, but he can't say why.</summary>
    public static readonly string[] VoteRight =
    {
        "Hm. {seat}. Fine. Back to work.",
        "{seat}? An odd choice. Never mind. Carry on.",
    };

    // ---------- Wireless: the ice warnings ----------

    /// <summary>
    /// The text on each ice warning (MSG 1-3), loosely after the real 14 April 1912 messages. Words in [brackets]
    /// arrive blacked out, more of them each time: a "something's off" moment.
    /// </summary>
    public static readonly string[] IceWarningText =
    {
        "MSG 1. Westbound steamers report bergs, growlers and field ice in 42 N, from 49 to 51 W.",
        "MSG 2. Large [icebergs] sighted. [Ice] [field] directly in your track. Advise [reduce] [speed].",
        "MSG 3. Saw much [heavy] [pack] [ice] and great number [large] [icebergs]. [Field] [ice] [ahead]. Weather clear.",
    };

    // ---------- Part 3: the Truth monologue ----------

    public static readonly string[] Monologue =
    {
        "You want to know why. Fine.",
        "No one remembers a careful captain.",
        "They remember the fastest crossing. And the name on the front page.",
        "I never promised you'd arrive. I promised we'd be {safe|remembered}.",
        "...",
        "Lookout's ringing. Your hands are on the wheel now. Not mine.",
    };
}
