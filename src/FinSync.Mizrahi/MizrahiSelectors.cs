namespace FinSync.Mizrahi;

internal static class MizrahiSelectors
{
    public const string OpenLoginButton = "#logInBtn";
    public const string LoginFrame = "#iframeLogIn";
    public const string UsernameInput = "#input_user";
    public const string PasswordInput = "#input_pass";
    public const string SubmitLoginButton = "#applogin-he";

    public const string OnlineBankingHost = "mto.mizrahi-tefahot.co.il";
    public const string LoginPagePath = "/login/index.html";
    public const string RejectedCredentialsText = "פרטי ההזדהות שהזנת שגויים";

    public const string CheckingAccountMenu = "a#mainMenu[href='/OnlineApp/osh/legacy/legacy-Osh-Main']";
    public const string CheckingAccountMenuActive = "aside.nav-item.active a[href='/OnlineApp/osh/legacy/legacy-Osh-Main']";
    public const string TransactionsMenu = "a[href='/OnlineApp/osh/legacy/root-main-osh-p428New']";
    public const string TransactionsMenuActive = "a.p-menuitem-link-active[href='/OnlineApp/osh/legacy/root-main-osh-p428New']";
    public const string TransactionsFrame = "#legacyIframe";

    public const string BetweenDatesButton = "button[ng-click^='vm.isBetween=!vm.isBetween']";
    public const string FromDateInput = "#dpFromDateK";
    public const string ToDateInput = "#dpToDateK";
    public const string ShowButton = "button[ng-click='ngFormMy.$valid && vm.rebindData()']";
    public const string PeriodSummary = ".row-sicum";

    public const string Grid = "#grid";
    public const string GridRows = "#grid tbody > tr.k-master-row";
    public const string EmptyPeriodMessage = "div[ng-show='vm.isGridEmpty && !vm.messages']";
    public const string AmountCell = "td.directionSwitch";
    public const string GroupHeaderCell = "td.seperate-td";
    public const string ExpandArrow = "a.expandItem";
    public const string DetailRowClass = "k-detail-row";
    public const string DetailPair = ".table-cell > div";
}
