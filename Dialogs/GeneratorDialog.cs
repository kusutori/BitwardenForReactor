using System;
using System.Globalization;
using BitwardenCli.Core.Generator;
using BitwardenForReactor.Application;
using BitwardenForReactor.Services;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using static BitwardenForReactor.Controls.Toolkit.SegmentedElement;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record GeneratorDialogProps(AppState State, Action<AppAction> Dispatch);

public sealed class GeneratorDialog : Component<GeneratorDialogProps>
{
    public override Element Render()
    {
        var (mode, setMode) = UseState(0);
        var (value, setValue) = UseState(string.Empty);
        var (length, setLength) = UseState("14");
        var (uppercase, setUppercase) = UseState(true);
        var (lowercase, setLowercase) = UseState(true);
        var (numbers, setNumbers) = UseState(true);
        var (special, setSpecial) = UseState(false);
        var (minimumNumbers, setMinimumNumbers) = UseState("1");
        var (minimumSpecial, setMinimumSpecial) = UseState("1");
        var (avoidAmbiguous, setAvoidAmbiguous) = UseState(false);
        var (words, setWords) = UseState("6");
        var (separator, setSeparator) = UseState("-");
        var (capitalizePassphrase, setCapitalizePassphrase) = UseState(false);
        var (numberPassphrase, setNumberPassphrase) = UseState(false);
        var (usernameType, setUsernameType) = UseState(0);
        var (capitalizeUsername, setCapitalizeUsername) = UseState(false);
        var (numberUsername, setNumberUsername) = UseState(false);
        var (email, setEmail) = UseState(Props.State.Status?.UserEmail ?? string.Empty);
        var (website, setWebsite) = UseState(string.Empty);

        async void Generate(int? nextMode = null)
        {
            var currentMode = nextMode ?? mode;
            string generated;

            try
            {
                generated = (currentMode switch
                {
                    1 => await BitwardenApplicationService.Instance.GeneratePassphraseAsync(new PassphraseGenerationOptions
                    {
                        Words = Clamp(ParseInt(words, 6), 3, 20),
                        Separator = NormalizeSeparator(separator),
                        Capitalize = capitalizePassphrase,
                        IncludeNumber = numberPassphrase
                    }),
                    2 => await BitwardenApplicationService.Instance.GenerateUsernameAsync(new UsernameGenerationOptions
                    {
                        Type = usernameType switch
                        {
                            1 => UsernameGenerationType.EmailPrefix,
                            2 => UsernameGenerationType.WebsitePrefix,
                            _ => UsernameGenerationType.RandomWord
                        },
                        Capitalize = capitalizeUsername,
                        IncludeNumber = numberUsername,
                        Email = email,
                        Website = website
                    }),
                    _ => await BitwardenApplicationService.Instance.GeneratePasswordAsync(new PasswordGenerationOptions
                    {
                        Length = Clamp(ParseInt(length, 14), 5, 128),
                        Uppercase = uppercase,
                        Lowercase = lowercase,
                        Numbers = numbers,
                        Special = special,
                        AvoidAmbiguous = avoidAmbiguous,
                        MinimumNumbers = numbers ? Math.Max(0, ParseInt(minimumNumbers, 0)) : null,
                        MinimumSpecial = special ? Math.Max(0, ParseInt(minimumSpecial, 0)) : null
                    })
                }) ?? string.Empty;
            }
            catch
            {
                Props.Dispatch(new NoticeShown(T("Generation failed"), T("Could not generate a value. Check the options and try again."), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error));
                return;
            }

            if (string.IsNullOrWhiteSpace(generated))
            {
                Props.Dispatch(new NoticeShown(T("Generation failed"), T("Could not generate a value. Check the options and try again."), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error));
                return;
            }

            setValue(generated);
        }

        UseEffect(() => Generate());

        var body = VStack(18,
            Segmented(items: new object[] { T("Password"), T("Passphrase"), T("Username") }, selectedIndex: mode, onSelectedIndexChanged: index =>
                {
                    setMode(index);
                    Generate(index);
                })
                .HorizontalAlignment(HorizontalAlignment.Stretch),
            ResultCard(value, () => Generate(), () => Copy(value)),
            TextBlock(T("Options")).SemiBold(),
            Options(mode,
                length, setLength,
                uppercase, setUppercase,
                lowercase, setLowercase,
                numbers, setNumbers,
                special, setSpecial,
                minimumNumbers, setMinimumNumbers,
                minimumSpecial, setMinimumSpecial,
                avoidAmbiguous, setAvoidAmbiguous,
                words, setWords,
                separator, setSeparator,
                capitalizePassphrase, setCapitalizePassphrase,
                numberPassphrase, setNumberPassphrase,
                usernameType, setUsernameType,
                capitalizeUsername, setCapitalizeUsername,
                numberUsername, setNumberUsername,
                email, setEmail,
                website, setWebsite),
            Button(HStack(6, TextBlock(T("Generator history")).SemiBold(), TextBlock("›")))
                .SubtleButton()
                .HorizontalAlignment(HorizontalAlignment.Stretch)
                .IsEnabled(false)
                .AutomationName(T("Generator history")))
            .Padding(24);

        var scroll = ScrollView(body)
            .HorizontalScrollMode(Microsoft.UI.Xaml.Controls.ScrollingScrollMode.Disabled)
            .Grid(row: 1);

        var content = Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
            Heading(T("Generator"))
                .Margin(left: 24, top: 20, right: 24, bottom: 12)
                .Grid(row: 0),
            scroll,
            Border(
                    HStack(12,
                            Button(T("Cancel"), () => Props.Dispatch(new GeneratorVisibilityChanged(false)))
                                .MinWidth(96)
                                .AutomationName(T("Cancel generator")))
                        .HorizontalAlignment(HorizontalAlignment.Left))
                .WithBorder(Theme.CardStroke, 1)
                .Padding(16)
                .Grid(row: 2));

        var dialog = Border(content)
            .Background(Theme.SolidBackground)
            .WithBorder(Theme.CardStroke, 1)
            .CornerRadius(8)
            .MinWidth(420)
            .MaxWidth(640)
            .MaxHeight(720)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .VerticalAlignment(VerticalAlignment.Stretch)
            .AutomationName(T("Generator"));

        return Border(
                dialog
                    .Margin(24)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center))
            .Background(Theme.SmokeFill)
            .AutomationName(T("Generator overlay"));
    }

    private Element ResultCard(string value, Action regenerate, Action copy) =>
        Border(
                Grid(
                    columns: [GridSize.Star(), GridSize.Auto],
                    rows: [GridSize.Auto],
                    TextBlock(string.IsNullOrWhiteSpace(value) ? T("Generating...") : value)
                        .FontFamily("Consolas")
                        .FontSize(20)
                        .TextWrapping()
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Grid(column: 0),
                    HStack(10,
                            Button(Icon(FontIcon("\uE72C", fontSize: 18)), regenerate)
                                .SubtleButton()
                                .ToolTip(T("Generate again"))
                                .AutomationName(T("Generate again")),
                            Button(Icon(FontIcon("\uE8C8", fontSize: 18)), copy)
                                .SubtleButton()
                                .IsEnabled(!string.IsNullOrWhiteSpace(value))
                                .ToolTip(T("Copy"))
                                .AutomationName(T("Copy generated value")))
                        .Grid(column: 1)))
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .CornerRadius(8)
            .Padding(28)
            .HorizontalAlignment(HorizontalAlignment.Stretch);

    private Element Options(
        int mode,
        string length, Action<string> setLength,
        bool uppercase, Action<bool> setUppercase,
        bool lowercase, Action<bool> setLowercase,
        bool numbers, Action<bool> setNumbers,
        bool special, Action<bool> setSpecial,
        string minimumNumbers, Action<string> setMinimumNumbers,
        string minimumSpecial, Action<string> setMinimumSpecial,
        bool avoidAmbiguous, Action<bool> setAvoidAmbiguous,
        string words, Action<string> setWords,
        string separator, Action<string> setSeparator,
        bool capitalizePassphrase, Action<bool> setCapitalizePassphrase,
        bool numberPassphrase, Action<bool> setNumberPassphrase,
        int usernameType, Action<int> setUsernameType,
        bool capitalizeUsername, Action<bool> setCapitalizeUsername,
        bool numberUsername, Action<bool> setNumberUsername,
        string email, Action<string> setEmail,
        string website, Action<string> setWebsite) =>
        mode switch
        {
            1 => PassphraseOptions(words, setWords, separator, setSeparator, capitalizePassphrase, setCapitalizePassphrase, numberPassphrase, setNumberPassphrase),
            2 => UsernameOptions(usernameType, setUsernameType, capitalizeUsername, setCapitalizeUsername, numberUsername, setNumberUsername, email, setEmail, website, setWebsite),
            _ => PasswordOptions(length, setLength, uppercase, setUppercase, lowercase, setLowercase, numbers, setNumbers, special, setSpecial, minimumNumbers, setMinimumNumbers, minimumSpecial, setMinimumSpecial, avoidAmbiguous, setAvoidAmbiguous)
        };

    private static Element PasswordOptions(
        string length, Action<string> setLength,
        bool uppercase, Action<bool> setUppercase,
        bool lowercase, Action<bool> setLowercase,
        bool numbers, Action<bool> setNumbers,
        bool special, Action<bool> setSpecial,
        string minimumNumbers, Action<string> setMinimumNumbers,
        string minimumSpecial, Action<string> setMinimumSpecial,
        bool avoidAmbiguous, Action<bool> setAvoidAmbiguous) =>
        VStack(18,
            Card(VStack(8,
                TextBox(length, setLength, header: T("Length")).AutomationName(T("Password length")),
                TextBlock(T("The value must be between 5 and 128. Use 14 or more characters for a strong password.")).Foreground(Theme.SecondaryText).TextWrapping())),
            Card(VStack(14,
                TextBlock(T("Include")).SemiBold(),
                HStack(24,
                    CheckBox(uppercase, setUppercase, "A-Z"),
                    CheckBox(lowercase, setLowercase, "a-z"),
                    CheckBox(numbers, setNumbers, "0-9"),
                    CheckBox(special, setSpecial, "!@#$%^&*")),
                Grid(
                    columns: [GridSize.Star(), GridSize.Star()],
                    rows: [GridSize.Auto],
                    TextBox(minimumNumbers, setMinimumNumbers, header: T("Minimum numbers")).Grid(column: 0).Margin(right: 8),
                    TextBox(minimumSpecial, setMinimumSpecial, header: T("Minimum special characters")).Grid(column: 1).Margin(left: 8)),
                CheckBox(avoidAmbiguous, setAvoidAmbiguous, T("Avoid ambiguous characters")))));

    private static Element PassphraseOptions(
        string words, Action<string> setWords,
        string separator, Action<string> setSeparator,
        bool capitalize, Action<bool> setCapitalize,
        bool includeNumber, Action<bool> setIncludeNumber) =>
        VStack(18,
            Card(VStack(8,
                TextBox(words, setWords, header: T("Number of words")).AutomationName(T("Number of words")),
                TextBlock(T("The value must be between 3 and 20. Use 6 or more words for a strong passphrase.")).Foreground(Theme.SecondaryText).TextWrapping())),
            Card(VStack(14,
                TextBox(separator, setSeparator, header: T("Word separator")).AutomationName(T("Word separator")),
                CheckBox(capitalize, setCapitalize, T("Capitalize")),
                CheckBox(includeNumber, setIncludeNumber, T("Include numbers")))));

    private static Element UsernameOptions(
        int usernameType, Action<int> setUsernameType,
        bool capitalize, Action<bool> setCapitalize,
        bool includeNumber, Action<bool> setIncludeNumber,
        string email, Action<string> setEmail,
        string website, Action<string> setWebsite) =>
        Card(VStack(14,
            ComboBox([T("Random word"), T("Email prefix"), T("Website prefix")], usernameType, setUsernameType)
                .Header(T("Type"))
                .AutomationName(T("Username type")),
            usernameType == 1 ? TextBox(email, setEmail, header: T("Email")).AutomationName(T("Username email")) : null,
            usernameType == 2 ? TextBox(website, setWebsite, header: T("Website")).AutomationName(T("Username website")) : null,
            CheckBox(capitalize, setCapitalize, T("Capitalize")),
            CheckBox(includeNumber, setIncludeNumber, T("Include numbers"))));

    private static Element Card(Element child) =>
        Border(child)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .CornerRadius(8)
            .Padding(24)
            .HorizontalAlignment(HorizontalAlignment.Stretch);

    private void Copy(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _ = AppCommands.CopyAsync(value, Props.Dispatch);
        }
    }

    private static int ParseInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private static string NormalizeSeparator(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "space" => " ",
            "empty" => string.Empty,
            "" => "-",
            _ => value
        };
}
