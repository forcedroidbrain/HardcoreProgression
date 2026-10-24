using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using HardcoreProgression;
using HardcoreProgression.Display;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json.Converters;
using SPTarkov.Server.Core.Utils.Json;
using Path = System.IO.Path;
using Settings = HardcoreProgression.Settings;

if (args.Length < 1) throw new ArgumentException("Pass the SPT root as the first argument.");
var sptRoot = Path.GetFullPath(args[0]);
var db = Path.Combine(sptRoot, "SPT_Runtime/SPT_Data/database");
_ = new JsonUtil([new SptJsonConverterRegistrator()]);
var options = JsonUtil.JsonSerializerOptionsNoIndent!;
StarterKitVerification.Run(db);
ReleaseVerification.Run(sptRoot);
var items = JsonSerializer.Deserialize<Dictionary<MongoId, TemplateItem>>(File.ReadAllText(Path.Combine(db,"templates/items.json")),options)!;
var settings = Settings.Load(AppContext.BaseDirectory);
var locale = Localization.Load(AppContext.BaseDirectory);
var templates = Activator.CreateInstance<TemplateTable>();
typeof(TemplateTable).GetProperty("Items")!.SetValue(templates, items);
typeof(TemplateTable).GetProperty("Quests")!.SetValue(templates, settings.Offers.Select(x=>x.Quest).Distinct().ToDictionary(x=>(MongoId)x,x=>(Quest)null!));
var traders = new TradersTable();
foreach(var dir in Directory.GetDirectories(Path.Combine(db,"traders")))
{
    if(!File.Exists(Path.Combine(dir,"assort.json"))) continue;
    var data = new Dictionary<string,JsonElement>();
    foreach(var name in new[]{"assort","base","questassort","dialogue"}) data[name]=JsonDocument.Parse(File.Exists(Path.Combine(dir,name+".json"))?File.ReadAllText(Path.Combine(dir,name+".json")):"{}").RootElement.Clone();
    var trader=JsonSerializer.Deserialize<Trader>(JsonSerializer.Serialize(data),options)!;
    traders[trader.Base.Id]=trader;
}
var logger=DispatchProxy.Create<ISptLogger<Progression>,SilentLogger>();
var fixtureHarmony=new Harmony("hardcoreprogression.tests");
fixtureHarmony.Patch(typeof(ProfileHelper).GetMethod("GetPmcProfile",[typeof(MongoId)])!,prefix:new HarmonyMethod(typeof(RaidFixture),nameof(RaidFixture.GetProfile)));
fixtureHarmony.Patch(typeof(MatchController).GetMethod("StartLocalRaidAsync")!,prefix:new HarmonyMethod(typeof(RaidFixture),nameof(RaidFixture.Start)));
fixtureHarmony.Patch(typeof(MatchController).GetMethod("EndLocalRaidAsync")!,prefix:new HarmonyMethod(typeof(RaidFixture),nameof(RaidFixture.End)));
var accessor=(ProfileHelper)RuntimeHelpers.GetUninitializedObject(typeof(ProfileHelper));
var progression=new Progression(templates,traders,accessor,new HttpResponseUtil(null!,null!),logger);
var ledgerPath=Path.Combine(AppContext.BaseDirectory,"raid-progress.json");
File.WriteAllText(ledgerPath,JsonSerializer.Serialize(new RaidProgress(),RaidLedger.Json));
await progression.OnLoadAsync(CancellationToken.None);
void Check(bool value,string message){if(!value)throw new Exception(message);}
Check(progression.Offers.Count==settings.Offers.Count,"Catalog entries were skipped");
Check(Harmony.GetPatchInfo(typeof(TradeHelper).GetMethod("BuyItem")!)?.Owners.Contains("com.forcedroidbrain.hardcoreprogression")==true,"Purchase hook missing");
Check(Harmony.GetPatchInfo(typeof(TraderAssortHelper).GetMethod("GetAssort")!)?.Owners.Contains("com.forcedroidbrain.hardcoreprogression")==true,"Assort hook missing");
Check(Harmony.GetPatchInfo(typeof(MatchController).GetMethod("StartLocalRaidAsync")!)?.Owners.Contains("com.forcedroidbrain.hardcoreprogression")==true,"Start raid hook missing");
Check(Harmony.GetPatchInfo(typeof(MatchController).GetMethod("EndLocalRaidAsync")!)?.Owners.Contains("com.forcedroidbrain.hardcoreprogression")==true,"End raid hook missing");
foreach(var offer in progression.Offers.Values)
{
    var trader=traders[(MongoId)offer.Trader];
    Check(trader.QuestAssort["started"][(MongoId)offer.Id].ToString()==offer.Quest,"Missing quest gate");
    Check(offer.Costs.All(x=>x.Count>0 && !Money.GetMoneyTpls().Contains((MongoId)x.Template)),"Currency or invalid barter cost");
    Check(!items[(MongoId)offer.Template].Properties!.QuestItem.GetValueOrDefault(),"Quest pickup offered");
    Check(trader.Assort.Items.Single(x=>x.Id.ToString()==offer.Id).Upd!.SpawnedInSession==false,"FIR offer");
    Check(trader.Assort.Items.Single(x=>x.Id.ToString()==offer.Id).Upd!.BuyRestrictionMax is null,"Trader restock counter can block an earned repeat exchange.");
    int count=trader.Assort.Items.Count;
    Policy.AddOffer(trader,offer);
    Check(trader.Assort.Items.Count==count,"Duplicate offer on repeated apply");
}
// Exercise the optional medicine rule on the real trader fixtures after checking standalone startup.
typeof(Progression).GetMethod("ApplyMedicineLimits",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(progression,null);
foreach(var medicine in settings.MedicineLimits)
{
    var cashListings=traders.SelectMany(t=>t.Value.Assort.Items.Where(x=>x.ParentId=="hideout" && x.Template.ToString()==medicine.Key)
        .Where(x=>t.Value.Assort.BarterScheme.TryGetValue(x.Id,out var s) && s[0].Any(c=>Money.GetMoneyTpls().Contains(c.Template)))
        .Select(x=>(Trader:t.Key,Item:x))).ToList();
    Check(cashListings.Count==1 && cashListings[0].Trader==Traders.THERAPIST,"Medicine cash cap can be bypassed");
    Check(cashListings[0].Item.Upd!.BuyRestrictionMax==medicine.Value,"Wrong medicine cap");
}

var gamma=(MongoId)"5857a8bc2459772bad15db29";
Check(items[gamma].Properties!.Grids!.Any(g=>g.Properties!.CellsH!=2 || g.Properties.CellsV!=2),"Default config unexpectedly shrank the Gamma.");
using(var fit=JsonDocument.Parse("[{\"_id\":\"aaaaaaaaaaaaaaaaaaaaaaaa\",\"_tpl\":\"5857a8bc2459772bad15db29\"},{\"_id\":\"bbbbbbbbbbbbbbbbbbbbbbbb\",\"_tpl\":\"593962ca86f774068014d9af\",\"parentId\":\"aaaaaaaaaaaaaaaaaaaaaaaa\",\"location\":{\"x\":1,\"y\":1,\"r\":\"Horizontal\"}}]"))
    Check(Progression.FitsSecureGrid(fit.RootElement,gamma.ToString(),items),"Valid saved key failed size check");
using(var overflow=JsonDocument.Parse("[{\"_id\":\"aaaaaaaaaaaaaaaaaaaaaaaa\",\"_tpl\":\"5857a8bc2459772bad15db29\"},{\"_id\":\"bbbbbbbbbbbbbbbbbbbbbbbb\",\"_tpl\":\"593962ca86f774068014d9af\",\"parentId\":\"aaaaaaaaaaaaaaaaaaaaaaaa\",\"location\":{\"x\":2,\"y\":0,\"r\":\"Horizontal\"}}]"))
    Check(!Progression.FitsSecureGrid(overflow.RootElement,gamma.ToString(),items),"Overflowing saved key allowed unsafe shrink");

var selected=progression.Offers.Values.GroupBy(x=>x.Quest).First(g=>g.Count()>1).First();
var sibling=progression.Offers.Values.First(o=>o.Quest==selected.Quest && o.Id!=selected.Id);
var other=progression.Offers.Values.First(o=>o.Quest!=selected.Quest);
MongoId session=(MongoId)"aaaaaaaaaaaaaaaaaaaaaaaa";
var profile=Activator.CreateInstance<PmcData>();
profile.Quests=[new QuestStatus{QId=(MongoId)selected.Quest,StartTime=1,Status=QuestStatusEnum.Started,StatusTimers=[]}];
profile.Inventory=Activator.CreateInstance<BotBaseInventory>();
profile.Inventory.Items=[];
RaidFixture.Profile=profile;
profile.Quests.Add(new QuestStatus{QId=(MongoId)selected.Quest,StartTime=1,Status=QuestStatusEnum.Started,StatusTimers=[]});
Check(Policy.ActiveQuests(profile).Count==1,"Duplicate active quest broke the raid snapshot");
Check(progression.GetQuestProgress(session).Quests[selected.Quest].Raids==0,"Duplicate active quest broke description progress");
profile.Quests.RemoveAt(1);
var description="Оригинальное описание\n<color=blue>Текст Expanded Task Text</color>";
Check(!progression.GetQuestProgress(session).Quests.ContainsKey(other.Quest),"Unrelated quest gained a description counter");
Check(!progression.GetQuestProgress(MongoId.Empty()).Available,"Missing session gained description progress");
var controller=(MatchController)RuntimeHelpers.GetUninitializedObject(typeof(MatchController));
int raidNumber=0;
async Task<string> Start(string side="pmc")
{
    RaidFixture.ServerId=$"factory4_day.{side} {++raidNumber}";
    var response=await controller.StartLocalRaidAsync(session,new(){Location="factory4_day",PlayerSide=side});
    return response.ServerId!;
}
Task Finish(string id,ExitStatus status=ExitStatus.SURVIVED)=>controller.EndLocalRaidAsync(session,new(){ServerId=id,Results=new(){Result=status}});
RaidProgress Read()=>JsonSerializer.Deserialize<RaidProgress>(File.ReadAllText(ledgerPath),RaidLedger.Json)!;
long Count()=>Read().Profiles.GetValueOrDefault(session.ToString())?.Quests.GetValueOrDefault(selected.Quest)?.CompletedRaids ?? 0;
for(int n=0;n<6;n++)
{
    Check(!progression.Eligible(profile,session,selected),$"Unlocked after only {n} raids");
    var view=progression.GetQuestProgress(session).Quests[selected.Quest];
    Check(view.Raids==n && view.Required==6 && !view.Unlocked,"Description progress differs from eligibility");
    string rendered=DescriptionBanner.Apply(description,view,locale.Languages["ru"]);
    Check(rendered.Contains($"Рейды: {n}/6") && rendered.EndsWith(description),"Counter text lost the original description");
    Check(DescriptionBanner.Apply(rendered,view,locale.Languages["ru"])==rendered,"Description counter duplicated");
    Check(DescriptionBanner.Remove(rendered)==description,"Counter removal damaged another mod's description");
    var id=await Start();
    Check(Count()==n,"Starting a raid counted as completing it");
    await Finish(id,n%2==0?ExitStatus.SURVIVED:ExitStatus.KILLED);
    Check(Count()==n+1,"Successful raid outcome was not counted");
    await Finish(id);
    Check(Count()==n+1,"Repeated end request counted twice");
}
Check(progression.Eligible(profile,session,selected),"Still locked after six completed raids");
Check(progression.GetQuestProgress(session).Quests[selected.Quest].Unlocked,"Description did not show unlocked state");
Check(progression.GetQuestProgress((MongoId)"bbbbbbbbbbbbbbbbbbbbbbbb").Quests[selected.Quest].Raids==0,"Counter leaked from another profile");
Check(progression.Eligible(profile,session,sibling),"Other required part of same quest did not unlock");
Check(!progression.Eligible(profile,(MongoId)"bbbbbbbbbbbbbbbbbbbbbbbb",selected),"Other profile gained unlock");
Check(!progression.Eligible(profile,session,other),"Inactive quest gained unlock");
var resumedLedger=new RaidLedger(ledgerPath);
Check(resumedLedger.IsEligible(session.ToString(),selected,1,6),"Unlock did not persist on ledger reload");
await Finish(await Start("savage")); Check(Count()==6,"Scav raid counted");
await Finish(await Start(),ExitStatus.TRANSIT); Check(Count()==6,"Transit inflated raid count");
await Finish(await Start(),ExitStatus.LEFT); Check(Count()==6,"Aborted raid counted");
await Start(); var replacement=await Start(); await Finish("factory4_day.pmc unknown");Check(Count()==6,"Unmatched result counted");
RaidFixture.EndFails=true;
try{await Finish(replacement);throw new Exception("Expected failed save");}catch(IOException){}finally{RaidFixture.EndFails=false;}
Check(Count()==6,"Failed save counted");
await Finish(replacement,ExitStatus.RUNNER);Check(Count()==7,"Run-through was not counted");
var restartPending=await Start();
Check(new RaidLedger(ledgerPath).CompleteRaid(session.ToString(),restartPending,Policy.ActiveQuests(profile),ExitStatus.MISSINGINACTION)==1,"Persisted pending raid not recovered");
Check(Count()==8,"MIA was not counted");
await Finish(restartPending);Check(Count()==8,"Recovered result counted twice");
RaidFixture.StartFails=true;
try{await Start();throw new Exception("Expected failed start");}catch(IOException){}finally{RaidFixture.StartFails=false;}
Check(Count()==8,"Failed start affected raid counter");
var delayedSaveRaid=await Start();
var saveCompletion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
RaidFixture.EndOverride=saveCompletion.Task;
var saving=Finish(delayedSaveRaid);
Check(!saving.IsCompleted && Count()==8,"Raid credited before asynchronous save completed");
saveCompletion.SetResult();await saving;RaidFixture.EndOverride=null;
Check(Count()==9,"Asynchronous save completion did not credit the raid");
Check(progression.GetQuestProgress(session).Quests[selected.Quest].Raids==6,"Display exceeded six raids");
foreach(var status in new[]{QuestStatusEnum.Locked,QuestStatusEnum.AvailableForStart,QuestStatusEnum.AvailableForFinish,QuestStatusEnum.Success,QuestStatusEnum.Fail})
{
    profile.Quests[0].Status=status;
    Check(!progression.Eligible(profile,session,selected),"Exchange allowed at quest status "+status);
    Check(!progression.GetQuestProgress(session).Quests.ContainsKey(selected.Quest),"Inactive quest retained description counter");
}
profile.Quests[0].Status=QuestStatusEnum.Started;
var request=new ProcessBuyTradeRequestData{ItemId=(MongoId)selected.Id,Count=2,TransactionId=(MongoId)selected.Trader};
bool fir=true;
var output=Activator.CreateInstance<ItemEventRouterResponse>();
Check(!progression.BeforeBuy(profile,request,session,ref fir,output,out var rejected),"Bulk exchange accepted");
Check(output.Warnings?.Count>0 && rejected is null,"Rejected purchase not reported cleanly");
request.Count=1;
Check(progression.BeforeBuy(profile,request,session,ref fir,output,out var state),"Valid exchange rejected");
Check(!fir,"Purchase retained FIR flag");
progression.AfterBuy(profile,state);
Check(progression.Eligible(profile,session,selected),"Failed purchase consumed raid unlock");
profile.Inventory.Items.Add(new Item{Id=(MongoId)"cccccccccccccccccccccccc",Template=(MongoId)selected.Template,Upd=new(){StackObjectsCount=1}});
progression.AfterBuy(profile,state);
Check(!progression.Eligible(profile,session,selected),"Successful purchase did not require six fresh raids");
var repeatView=progression.GetQuestProgress(session).Quests[selected.Quest];
Check(repeatView.Raids==6 && repeatView.Unlocked && repeatView.Repeats.Count==1 && repeatView.Repeats[0].Raids==0,"Repeat description did not reset for the purchased item");
Check(DescriptionBanner.Apply(description,repeatView,locale.Languages["ru"]).Contains("Повторный обмен"),"Repeat cooldown is invisible");
Check(progression.Eligible(profile,session,sibling),"Buying a part locked other quest parts");
for(int n=0;n<6;n++)
{
    Check(!progression.Eligible(profile,session,selected),"Old surplus raids bypassed cooldown");
    Check(progression.GetQuestProgress(session).Quests[selected.Quest].Repeats.Single().Raids==n,"Repeat display drifted from raid count");
    await Finish(await Start());
}
Check(progression.Eligible(profile,session,selected),"Lost-item recovery stayed locked after another six raids");
Check(progression.GetQuestProgress(session).Quests[selected.Quest].Repeats.Count==0,"Unlocked repeat counter was not cleared");
// A newly accepted/restarted quest cannot inherit the previous assignment's raid count.
profile.Quests[0].StartTime=2;
Check(!progression.Eligible(profile,session,selected),"Restarted quest inherited old raid credit");
Check(progression.GetQuestProgress(session).Quests[selected.Quest].Raids==0,"Restarted description inherited old counter");
var restarted=await Start();profile.Quests[0].StartTime=3;await Finish(restarted);
Check(!progression.Eligible(profile,session,selected),"Quest changed during raid gained credit");
profile.Quests[0].Status=QuestStatusEnum.AvailableForStart;
var notAccepted=await Start();profile.Quests[0].Status=QuestStatusEnum.Started;await Finish(notAccepted);
Check(!progression.Eligible(profile,session,selected),"Quest accepted after entering counted");
for(int n=0;n<6;n++)await Finish(await Start());
Check(progression.Eligible(profile,session,selected),"New quest assignment failed to unlock");
profile.Quests[0].Status=QuestStatusEnum.Success;
Check(!progression.Eligible(profile,session,selected),"Completed quest still offered exchange");
profile.Quests[0].Status=QuestStatusEnum.Started;
File.WriteAllText(ledgerPath,"not valid json");
Check(!progression.Eligible(profile,session,selected),"Corrupt ledger unlocked offers");
Check(!progression.GetQuestProgress(session).Available,"Corrupt journal displayed a fake zero counter");
File.WriteAllText(ledgerPath,JsonSerializer.Serialize(new RaidProgress(),RaidLedger.Json));
var unsafeName=new QuestProgressInfo{Raids=3,Required=6,Repeats=[new(){ItemName="<color=red>Part</color>",Raids=2,Required=6}]};
Check(!DescriptionBanner.Apply("base",unsafeName,locale.Languages["ru"]).Contains("<color=red>"),"Item name injected rich text");
Check(DescriptionBanner.Apply("base",unsafeName,locale.Languages["en"]).Contains("Raids: 3/6"),"English counter missing");
foreach (string language in new[] { "en", "ru" })
{
    var strings = locale.Languages[language];
    var view = new QuestProgressInfo { Raids=1, Required=1, Unlocked=true,
        Repeats=[new() { ItemName="Part\r\n\n<name>", Raids=0, Required=1 }] };
    string plain=DescriptionBanner.Apply(description,view,strings,richText:false);
    string banner=plain[..^description.Length];
    Check(!banner.Contains('<') && !banner.Contains('>'),"Plain-text task panel received markup");
    Check(banner.Contains(strings["Raids"]+"1/1") && banner.Contains(strings["Repeat"]) && banner.Contains("0/1"),"Plain counter lost unlocked/repeat progress");
    Check(DescriptionBanner.Remove(plain)==description,"Plain counter damaged the original description");
    Check(DescriptionBanner.Apply(plain,view,strings,false)==plain,"Plain counter duplicated");
    string rich=DescriptionBanner.Apply(plain,view,strings,true);
    Check(DescriptionBanner.Remove(rich)==description,"Switching to rich text retained an old plain block");
    Check(DescriptionBanner.Apply(rich,view,strings,false)==plain,"Switching to plain text retained rich markup");
}
fixtureHarmony.UnpatchSelf();
if(args.Length > 1) IntegrationVerification.Run(sptRoot,args[1],templates,traders,progression);
Console.WriteLine($"PASS: {progression.Offers.Count} real-data offers; actual patched async MatchController calls; six completed raids; deaths, run-through, MIA; duplicate results; scav/transit/aborts; failed start/save; persistence and pending recovery; quest assignment changes; per-item repeat cooldown; bulk/FIR rules; default optional-rule isolation/secure-grid safeguards; corrupt ledger fails closed.");
Console.WriteLine("PASS descriptions: 0–6 counters; active quests only; profile/assignment isolation; repeat cooldown; Russian/English rich/plain text and mode switches; original description and other markup preserved; no duplicate block; corrupt ledger hidden.");

public static class RaidFixture
{
    public static PmcData Profile=null!;
    public static string ServerId="";
    public static bool StartFails,EndFails;
    public static Task? EndOverride;
    public static bool GetProfile(ref PmcData __result){__result=Profile;return false;}
    public static bool Start(ref Task<StartLocalRaidResponseData> __result)
    {__result=StartFails?Task.FromException<StartLocalRaidResponseData>(new IOException("Test start failure")):Task.FromResult(new StartLocalRaidResponseData{ServerId=ServerId});return false;}
    public static bool End(ref Task __result)
    {__result=EndOverride ?? (EndFails?Task.FromException(new IOException("Test save failure")):Task.CompletedTask);return false;}
}

public class SilentLogger : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => method?.ReturnType==typeof(bool)?false:null;
}
