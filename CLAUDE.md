# OpenAdoration — Claude Context File
# Last updated: 2026-05-29
# Purpose: Full project context in one read. Read before touching any code.

---

code_standards:
  status: MANDATORY — non-negotiable on every change.
  principles:
    SOLID:
      S: ViewModels=state+commands only; services=business logic; repos=data access. Never mix.
      O: Extend via new classes/interfaces. New types slot in without touching existing code.
      L: Implementations fully substitutable. No NotImplementedException in interface methods.
      I: Narrow interfaces. Callers never depend on methods they don't use. Split fat interfaces.
      D: WPF → Application interfaces only. Never reference Infrastructure from WPF or Application.
    clean_code:
      - No dead code, commented-out blocks, or TODO/FIXME in committed code.
      - Methods < 20 lines; extract named private helpers otherwise.
      - No magic strings/numbers. Validate at system boundaries only; trust internals.
    reliability:
      - All IDisposable classes implement Dispose() and unsubscribe events.
      - UI updates from non-UI-thread → Dispatcher.Invoke.
      - CancellationToken propagated through all async chains.
      - EF Core SaveChangesAsync is transactional; never call it twice per logical operation.
    patterns_enforced:
      - Scope-per-navigation (G11). IsBusy guard (G4). Dispatcher.Invoke in singletons. IDisposable on VMs.
  what_not_to_do:
    - No error handling for impossible code paths. No backwards-compat shims. No hypothetical future design.
    - Never inject concrete classes across layer boundaries.

---

project:
  name: OpenAdoration
  description: Free open-source Windows desktop worship-presentation app. Fully offline — SQLite only.
  platform: Windows 10+; target_framework: net10.0-windows; solution: OpenAdoration.sln
  db_path: "%LOCALAPPDATA%\\OpenAdoration\\openadoration.db"
  log_dir: "%LOCALAPPDATA%\\OpenAdoration\\logs"

tech_stack:
  ui: WPF (.NET 10)
  orm: Entity Framework Core 9 + SQLite
  mvvm: CommunityToolkit.Mvvm 8.4.0  # source-generated [ObservableProperty], [RelayCommand]
  di: Microsoft.Extensions.Hosting (generic host)
  logging: Serilog (rolling daily file + debug sink)
  multi_monitor: System.Windows.Forms.Screen.AllScreens  # requires UseWindowsForms=true in .csproj
  nuget_packages:
    Microsoft.Extensions.Hosting: "9.0.4"
    Microsoft.EntityFrameworkCore + Sqlite + Design: "9.0.4"  # Design PrivateAssets=all; MUST be in BOTH Infrastructure+WPF (G2)
    CommunityToolkit.Mvvm: "8.4.0"
    Serilog + Extensions.Logging + Sinks.File + Sinks.Debug: "4.2.0/9.0.0/6.0.0/3.0.0"
    Extended.Wpf.Toolkit: "5.0.0"  # xctk:ColorPicker in AddEditThemeView.xaml

# ─────────────────────────────────────────────────────────────────────────────
architecture:
  pattern: Clean Architecture (Domain → Application → Infrastructure ← WPF)
  dependency_rule: WPF → Application only. Infrastructure → Application only. Domain → nothing.

  layers:
    Domain: OpenAdoration.Domain — Entities, Enums, BaseEntity(Id/CreatedAt/UpdatedAt)
    Application: OpenAdoration.Application — Service+Repo interfaces, Slide DTO, SlideContext, SlideType
    Infrastructure: OpenAdoration.Infrastructure — EF Core DbContext, repos, migrations, logging
    WPF: OpenAdoration.WPF — App/Windows, ViewModels, Views, Converters, Helpers, Styles

  navigation_pattern: >
    ContentControl Content="{Binding CurrentView}". App.xaml maps ViewModel → View UserControl.
    MainViewModel.NavigateTo<T>(): creates IServiceScope, resolves VM, assigns CurrentView, disposes old scope.
    Views fire LoadCommand from Loaded event in code-behind.

  di_lifetime_rules:
    singletons: MainViewModel, MainWindow, ProjectionWindow, IProjectionService, ITokenResolver
    transients: SongsViewModel, BibleViewModel, ServiceScheduleViewModel, MediaViewModel, ThemeViewModel,
                AddEditSongViewModel, AddEditThemeViewModel, StageViewModel
    scoped: all services (ISongService, IBibleService, IThemeService, IMediaService, IWorshipServiceService)
            + all repos (ISongRepository, IBibleRepository, IThemeRepository, IMediaRepository, IWorshipServiceRepository)
    factory: IDbContextFactory<AppDbContext> — AddDbContextFactory (singleton factory, scoped context)

  scope_per_navigation: >
    CRITICAL: MainViewModel._currentScope (IServiceScope?) disposed+replaced on every NavigateTo<T>().
    NEVER call GetRequiredService<T>() on root IServiceProvider for page-level ViewModels.

# ─────────────────────────────────────────────────────────────────────────────
database:
  engine: SQLite
  migration_command: "dotnet ef migrations add <Name> --project OpenAdoration.Infrastructure --startup-project OpenAdoration.WPF"
  migrations_applied: automatically at startup via MigrateAsync()
  current_migration: 20260529012740_AddScheduleItemAutoAdvance
  migrations_history:
    - 20260505012006_InitialCreate
    - 20260511000000_AddSongClassification
    - 20260518_AddThemeVideoBackground
    - 20260519005541_AddThemeTextAlignment
    - 20260520041713_AddBibleVersesFts
    - AddSongVerseOrder
    - AddSongSectionsFts
    - AddThemeHeaderFooter
    - 20260529011841_AddSongCopyrightAndCcli
    - 20260529012740_AddScheduleItemAutoAdvance

  tables:
    Songs: Id, Title(NOT NULL), Author(NULL), Classification(NULL), VerseOrder(NULL),
           Copyright(NULL), CcliNumber(NULL), CreatedAt, UpdatedAt
    SongSections: Id, SongId(FK→Songs CASCADE), Type(INT), SectionNumber, Lyrics(NOT NULL), Order(NOT NULL), CreatedAt, UpdatedAt
    SongSectionsFts: FTS5 virtual table — Lyrics indexed; rowid=SongSections.Id; 3 sync triggers (Insert/Update/Delete)
    Themes: Id, Name, FontFamily, FontSize, FontColor, BackgroundColor, BackgroundImagePath(NULL),
            BackgroundVideoPath(NULL), TextAlignment(TEXT default "Center"),
            HeaderTemplate(NULL), FooterTemplate(NULL), IsDefault, CreatedAt, UpdatedAt
      seed: Id=1 Arial 48pt white/black IsDefault=true — STATIC date new DateTime(2025,1,1,0,0,0,Utc) never DateTime.UtcNow (G3)
    BibleVersions: Id, Name, Abbreviation, Language, CreatedAt, UpdatedAt
    BibleBooks: Id, BibleVersionId(FK), Name, Abbreviation, Testament(INT), BookNumber, ChapterCount, CreatedAt, UpdatedAt
    BibleVerses: Id, BibleVersionId, Book, Chapter, Verse, Text, CreatedAt, UpdatedAt
    BibleVersesFts: FTS5 virtual table — Text indexed, BibleVersionId UNINDEXED; rowid=BibleVerses.Id; tokenize=unicode61
    WorshipServices: Id, Name, Date, CreatedAt, UpdatedAt
    ScheduleItems: TPH discriminator ItemType("Song"|"Bible"|"Media"); (ServiceId,Order) composite index
      base_extra: AutoAdvanceSeconds(NULL INT) — null/0=manual; positive=seconds between slide advances
      song_extra: SongId(FK); bible_extra: Book,Chapter,VerseStart,VerseEnd,BibleVersionId(NULL FK); media_extra: MediaFileId(FK)
    MediaFiles: Id, FileName, FilePath, Type(INT: Image=0 Video=1), CreatedAt, UpdatedAt

  key_patterns:
    timestamp: StampTimestamps() — Added sets CreatedAt+UpdatedAt; Modified sets only UpdatedAt.
    update_song: Load existing tracked → RemoveRange(sections) → add incoming Id=0 (forces INSERT). Never reuse Ids.
    bible_import: Batch 1000 rows + ChangeTracker.Clear() per batch. Required — full Bible ≈ 31,000 verses.
    song_search: Two-step — title/author LIKE first; lyrics FTS5 (SongSectionsFts) as fallback when 0 results.
      FTS term escaping: each word gets trailing * for prefix matching ("cura" matches "curará").

# ─────────────────────────────────────────────────────────────────────────────
projection_engine:
  class: ProjectionService (IProjectionService, Singleton)
  api:
    LoadSlides(slides, contextLabel): starts projection; fires SlideChanged(slides[0]) + ProjectionStateChanged(true)
    Next/Previous/GoTo(index): advances/decrements/jumps; fires SlideChanged
    ShowBlank(): fires SlideChanged(Slide.Blank()) without stopping
    Stop(): clears state + IsServiceScheduleActive + NextScheduleItemPreviewSlide; fires events
    RequestNextScheduleItem() / RequestPreviousScheduleItem(): message-bus; ServiceScheduleViewModel handles
    SetServiceScheduleActive(bool): called by ServiceScheduleViewModel on StartLive/StopLive/Dispose
    SetNextScheduleItemPreview(Slide?): set after each LoadSlidesForCurrentItemAsync; used by StageViewModel
  events:
    SlideChanged(Slide?), ProjectionStateChanged(bool), ThemeChanged
    NextScheduleItemRequested, PreviousScheduleItemRequested  # fired by StageViewModel; handled by ServiceScheduleVM
    ServiceScheduleActiveChanged  # StageViewModel shows Prev/Next Item buttons only when true
    NextScheduleItemPreviewChanged  # StageViewModel subscribes to refresh UP NEXT panel
  safety: Each subscriber wrapped in try/catch; crash never stops engine.
  properties: CurrentSlide, CurrentSlides, CurrentSlideIndex, IsProjecting, ContextLabel,
              IsServiceScheduleActive, NextScheduleItemPreviewSlide

  slide_dto: Content(string), Type(SlideType), Label(string), MediaPath(string?), ThemeId(int?), Context(SlideContext)
    Slide.Blank(): factory; constructor exempts Blank+Media from content requirement.
    SlideContext: SongTitle, SongAuthor, SongVerseTag, SongCopyright, SongCcliNumber,
                  BibleBookName, BibleChapterId, BibleVerseId, BibleReference, BibleDescription
  slide_types: Song, Bible, Media, Blank

  projection_window:
    shows_on: secondary monitor (fallback primary); hidden at startup; shown on first projection or "Open Screen" click
    layout: 3-zone Grid — Header(Auto) / Body(*Viewbox) / Footer(Auto)
      Header: TextBlock x:Name=HeaderText; resolved from Theme.HeaderTemplate + ITokenResolver
      Body: Viewbox → TextBlock x:Name=SlideTextBlock; theme font/size/color/alignment applied
      Footer: TextBlock x:Name=FooterText; resolved from Theme.FooterTemplate + ITokenResolver
    zone_visibility: zone shown only if resolved text contains at least one letter or digit (auto-hide for empty tokens)
    CornerLabel: fallback (top-left ZIndex=100) used only when no HeaderTemplate set on active theme
    Media: BitmapImage → BackgroundImage (image) or MediaElement ContentVideo (video, with audio)
    Blank: BlankOverlay rectangle visible
    Theme resolution: per-slide via IServiceScopeFactory → IThemeService; ConcurrentDictionary cache per session
    cleanup: OnClosed() unsubscribes SlideChanged + ProjectionStateChanged + ThemeChanged (G9)

# ─────────────────────────────────────────────────────────────────────────────
token_system:
  resolver: ITokenResolver → TokenResolver (sealed partial; [GeneratedRegex] for \[(\w+)\] pattern)
  registration: AddSingleton<ITokenResolver, TokenResolver>() in InfrastructureServiceExtensions
  context_source: SlideContext built in SongService.GenerateSlides() and BibleService.GenerateSlide()
  resolve_call: ProjectionWindow.ShowText() and StageViewModel.BuildPreview() both call _tokenResolver.Resolve()

  tokens:
    "[SongTitle]"      → SlideContext.SongTitle
    "[SongAuthor]"     → SlideContext.SongAuthor
    "[SongVerseTag]"   → SlideContext.SongVerseTag  # e.g. "Verse 1", "Chorus"
    "[SongCopyright]"  → SlideContext.SongCopyright
    "[SongCCLI]"       → SlideContext.SongCcliNumber
    "[BibleBookName]"  → SlideContext.BibleBookName
    "[BibleChapterID]" → SlideContext.BibleChapterId  # raw number e.g. "3"
    "[BibleVerseID]"   → SlideContext.BibleVerseId    # raw number/range e.g. "16" or "16-18"
    "[BibleReference]" → SlideContext.BibleReference  # formatted "John 3:16" or "John 3:16-18"
    "[BibleDescription]" → SlideContext.BibleDescription  # Bible version name e.g. "King James Version"
    unknown tokens: left unchanged in output

  zone_auto_hide: >
    After resolving, ProjectionWindow and StageViewModel check resolved.Any(char.IsLetterOrDigit).
    If false → zone hidden. Handles pure-token templates on wrong slide type (e.g. Bible header on song slide).
    Static text like "Community Church" always shows. Mixed templates with static text + empty tokens stay visible.

  template_storage: Theme.HeaderTemplate (NULL TEXT), Theme.FooterTemplate (NULL TEXT)
  ui_chips: AddEditThemeView.xaml — clickable chip buttons insert token at cursor via code-behind InsertToken()

# ─────────────────────────────────────────────────────────────────────────────
stage_view:
  class: StageView (UserControl) + StageViewModel (Transient, IDisposable)
  nav_button: "📺  Stage View" in MainWindow sidebar → NavigateToStageCommand
  datatemplate: registered in App.xaml like all other page VMs

  layout:
    status_bar: LIVE/STOPPED badge + ContextLabel + SlidePosition + Prev/Next Item buttons
    left_panel (2/3): current slide — Viewbox(1920×1080) with same 7-layer stack as ProjectionWindow
    right_panel (1/3): UP NEXT — smaller Viewbox; "End of item" placeholder when nothing follows

  preview_rendering (both panels):
    Layer 1: Rectangle Fill=BgColor
    Layer 2: Image BgImagePath (FilePathToImage converter)
    Layer 3: Border "🎬 Video background active" when HasBgVideo
    Layer 4: 3-zone Grid (Header/Body Viewbox/Footer) when IsText
    Layer 5: Image MediaPath when IsImageMedia
    Layer 6: MediaElement (LoadedBehavior=Manual, IsMuted=True) when IsVideoMedia — code-behind SyncVideo()
    Layer 7: Rectangle Fill=Black when IsBlank
    Layer 8: "Not projecting" dim overlay when !IsProjecting (current panel only)

  cross_item_up_next: >
    When nextIdx >= slides.Count AND IsProjecting AND NextScheduleItemPreviewSlide is set,
    StageViewModel shows the first slide of the next schedule item as UP NEXT.
    ServiceScheduleViewModel calls GetNextItemFirstSlideAsync() after each LoadSlidesForCurrentItemAsync()
    and pushes result via IProjectionService.SetNextScheduleItemPreview().

  schedule_buttons: >
    "◀ Prev Item" / "Next Item ▶" visible only when IProjectionService.IsServiceScheduleActive.
    Commands call RequestPreviousScheduleItem() / RequestNextScheduleItem() on IProjectionService;
    ServiceScheduleViewModel handles the events and calls PrevItem() / NextItem() if live.

  SlidePreview_record: immutable record; VM swaps whole object → WPF re-evaluates all {Binding CurrentPreview.X}
  video_sync: StageView code-behind subscribes to VM.PropertyChanged; on CurrentPreview/NextPreview change → SyncVideo()

  events_subscribed: SlideChanged, ProjectionStateChanged, ThemeChanged, ServiceScheduleActiveChanged, NextScheduleItemPreviewChanged
  disposal: Dispose() unsubscribes all 5 events; called by scope disposal on navigation away

# ─────────────────────────────────────────────────────────────────────────────
auto_advance:
  field: ScheduleItem.AutoAdvanceSeconds (int?, NULL=manual, 0=manual, positive=seconds)
  ui: builder item rows show [−] [⏱ Manual/Ns] [+]; +5s increments, max 300s
  persistence: ScheduleItemViewModel fires AutoAdvanceChangeRequested → ServiceScheduleViewModel calls SetItemAutoAdvanceAsync

  timer_behavior: >
    DispatcherTimer (UI thread). Always one-shot — stopped before advancing so SlideChanged restarts it.
    SlideChanged subscription in ServiceScheduleViewModel resets timer on every slide change
    (manual advance via Space key also resets countdown, preventing unexpectedly short intervals).
    OnAutoAdvanceTick: if not last slide → Next(); elif CanNextItem → NextItem(); else stop.
  stop_conditions: StopLive(), OnProjectionStateChanged(false), Dispose() — G19

# ─────────────────────────────────────────────────────────────────────────────
song_import:
  formats:
    OpenLyrics XML: OA Helper — OpenLyricsParser.cs; parses title, author, copyright, ccliNo, verseOrder, verses
      section name mapping: v{n}→Verse, c→Chorus, p→PreChorus, b→Bridge, i→Intro, e→Outro, t→Tag
      verseOrder normalized to OA uppercase token format (V1 C V2 → V1 C V2)
  ui: SongsView.xaml has "Import" button → ImportSongCommand in SongsViewModel
  note: Only OpenLyrics supported so far. VP reference lists 20+ VP formats for future consideration.

# ─────────────────────────────────────────────────────────────────────────────
bible_import:
  entry_point: BibleFormatDispatcher.Import(filePath) — returns BibleImportResult(Version, Books, Verses)

  dispatcher_logic: >
    .xml → PeekXmlRoot() → route by root element name.
    .json → JsonDocument: Array→thiagobodruk; {metadata+verses}→BibleSuperSearchJson (FIRST); {books} arrays-of-arrays→thiagobodruk; arrays-of-objects→OpenAdoration.
    .zip → BibleSuperSearchZip. .sqlite → BibleSuperSearchSqlite.
    Unknown extension → sniff: '<'→XML; else→JSON.

  supported_formats:
    Zefania_XML: roots=[XMLBIBLE,ZEFANIA,...]; parser=ZefaniaXmlParser(XDocument); book names from bname attr (localized)
    OSIS_XML: root=osis; parser=OsisXmlParser(XmlReader streaming); milestone+container verse styles; book name from <title> (localized, OsisBookCatalog fallback)
    USFX_XML: root=usfx; parser=UsfxXmlParser(XmlReader streaming); book name from <h>/<toc level=2> (localized, OsisBookCatalog fallback)
    thiagobodruk_JSON: root=Array or {books}; chapters=array-of-arrays(0-indexed); parser=ThiagobodrukJsonParser
    OpenAdoration_JSON: root={name,abbreviation,language,books:[{name,chapters:[{number,verses:[{number,text}]}]}]}; parser=OpenADorationJsonParser
    BibleSuperSearch_JSON: root={metadata,verses:[{book_name,book(int 1-66),chapter,verse,text}]}; parser=BibleSuperSearchJsonParser; uses book_name when present (localized), falls back to OsisBookCatalog.GetByNumber
    BibleSuperSearch_ZIP: info.json(metadata+fields+delimiter) + verses.txt(pipe-delimited); parser=BibleSuperSearchZipParser; integer book numbers only → English from GetByNumber
    BibleSuperSearch_SQLite: meta(field,value) + verses(id,book,chapter,verse,text); parser=BibleSuperSearchSqliteParser; integer book numbers only → English from GetByNumber

  osis_book_catalog: >
    OsisBookCatalog: static dict 66 books. Key=OSIS/USFX ID (Gen,Exod,Matt...). Value=BookInfo(Name,Abbreviation,Number,Testament).
    GetOrFallback(id, fallbackNumber, fallbackName) — safe for non-standard IDs.
    GetByNumber(int 1-66) — lazy reverse dict; used by BSS parsers.
    Testament values: Old/New (NOT OldTestament/NewTestament — G14).

# ─────────────────────────────────────────────────────────────────────────────
key_files:  # ★ = read first when debugging

  WPF:
    - OpenAdoration.WPF/App.xaml.cs — DI root, host startup, DB init; registers ITokenResolver singleton
    - OpenAdoration.WPF/App.xaml — DataTemplate nav map + global converters; every navigated VM needs entry (G8)
    - OpenAdoration.WPF/MainWindow.xaml — shell: 200px sidebar (Songs/Bible/Schedule/Media/Themes/Stage) + ContentControl + bottom projection bar (no thumbnail)
    - OpenAdoration.WPF/ProjectionWindow.xaml.cs  ★ — 3-zone rendering, ITokenResolver, theme cache, Dispatcher.Invoke
    - OpenAdoration.WPF/ViewModels/MainViewModel.cs  ★ — scope-per-nav, projection controls, NavigateToStageCommand
    - OpenAdoration.WPF/ViewModels/StageViewModel.cs  ★ — SlidePreview record; themed previews; cross-item UP NEXT
    - OpenAdoration.WPF/Views/StageView.xaml + .cs  ★ — 2-panel layout; MediaElement video sync in code-behind
    - OpenAdoration.WPF/ViewModels/SongsViewModel.cs  ★ — CRUD + two-step search + projection + import
    - OpenAdoration.WPF/ViewModels/BibleViewModel.cs  ★ — cascade; import; SelectedChapter=0 sentinel (G16)
    - OpenAdoration.WPF/ViewModels/AddEditThemeViewModel.cs  ★ — BackgroundType; TextAlignment; HeaderTemplate/FooterTemplate
    - OpenAdoration.WPF/ViewModels/ServiceScheduleViewModel.cs  ★ — service list + builder + live mode + auto-advance timer
    - OpenAdoration.WPF/ViewModels/ScheduleItemViewModel.cs — AutoAdvanceSeconds; IncreaseAutoAdvance/DecreaseAutoAdvance commands
    - OpenAdoration.WPF/Views/BibleView.xaml  ★ — 3-column browser; CollectionViewSource grouped by Testament; chapter WrapPanel
    - OpenAdoration.WPF/Views/ServiceScheduleView.xaml  ★ — 3-panel (list/builder/live); [−][⏱][+] auto-advance controls in builder
    - OpenAdoration.WPF/Views/AddEditThemeView.xaml  ★ — header/footer template boxes + full token chip row
    - OpenAdoration.WPF/Helpers/BibleImport/BibleFormatDispatcher.cs  ★ — format auto-detection + dispatch
    - OpenAdoration.WPF/Helpers/SongImport/OpenLyricsParser.cs — OpenLyrics XML → Song (title/author/copyright/ccliNo/verseOrder/sections)
    - OpenAdoration.WPF/Styles/Base.xaml  ★ — all control styles; FieldComboBox full ControlTemplate (G10)
    - OpenAdoration.Tests.Infrastructure/BibleImport/BibleParserTests.cs — 8/8 format tests

  Application:
    - OpenAdoration.Application/Common/Slide.cs  ★ — projection DTO; Blank() factory; SlideContext attached
    - OpenAdoration.Application/Common/SlideContext.cs — token resolution bag; all song+bible token fields
    - OpenAdoration.Application/Services/IProjectionService.cs + ProjectionService.cs  ★ — full event bus
    - OpenAdoration.Application/Services/ITokenResolver.cs + TokenResolver.cs — [GeneratedRegex] token resolver
    - OpenAdoration.Application/Services/ISongService.cs — includes SearchByLyricsAsync
    - OpenAdoration.Application/Services/IBibleService.cs — GenerateSlide now accepts BibleVersion? for [BibleDescription]

  Infrastructure:
    - OpenAdoration.Infrastructure/Persistence/AppDbContext.cs  ★ — StampTimestamps(), ApplyConfigurationsFromAssembly
    - OpenAdoration.Infrastructure/Repositories/SongRepository.cs  ★ — FTS lyrics search; replace-all-sections pattern
    - OpenAdoration.Infrastructure/Repositories/BibleRepository.cs  ★ — batched 1000-row import + FTS sync
    - OpenAdoration.Infrastructure/Repositories/WorshipServiceRepository.cs  ★ — SetItemAutoAdvanceAsync; GetWithItemsAsync
    - OpenAdoration.Infrastructure/Configurations/ScheduleItemConfiguration.cs  ★ — TPH discriminator
    - OpenAdoration.Infrastructure/Extensions/InfrastructureServiceExtensions.cs  ★ — AddInfrastructure(); ITokenResolver

  Domain:
    - OpenAdoration.Domain/Entities/Song.cs — Title, Author, Classification, VerseOrder, Copyright, CcliNumber, Sections; GetOrderedSections()
    - OpenAdoration.Domain/Entities/ScheduleItem.cs — base: Order, AutoAdvanceSeconds(NULL), ThemeId(NULL)
    - OpenAdoration.Domain/Entities/Theme.cs — all theme fields including HeaderTemplate, FooterTemplate
    - OpenAdoration.Domain/Enums/SectionType.cs — [Verse, Chorus, PreChorus, Bridge, Intro, Outro, Tag]
    - OpenAdoration.Domain/Enums/Testament.cs — [Old, New]  # CRITICAL: NOT OldTestament/NewTestament (G14)

# ─────────────────────────────────────────────────────────────────────────────
styles:
  colors:  # Colors.xaml
    Primary: "#1E1E2E", Surface: "#2A2A3E", Accent: "#7C6AF7", AccentHover: "#9B8FFF"
    Danger: "#E05252", TextPrimary: "#FFFFFF", TextSecondary: "#A0A0B8", Border: "#3A3A52", ActiveNav: "#3A3A5C"

  key_styles:  # Base.xaml
    buttons: PrimaryButton, SecondaryButton, DangerButton, IconButton, IconDangerButton, NavButton, ProjectionButton
    textboxes: FieldTextBox (placeholder via Tag prop), SearchTextBox, LyricsTextBox
    combobox: FieldComboBox — full ControlTemplate on ItemContainerStyle required (G10); use ItemTemplate not DisplayMemberPath with custom template
    cards: SongRowCard, SectionCard
    togglebuttons: AlignToggleButton — IsChecked Mode=OneWay mandatory (G17)
    date: DatePicker + DatePickerTextBox both need full ControlTemplates (dark theme); use PART_TextBox, PART_Button, PART_Popup named parts

# ─────────────────────────────────────────────────────────────────────────────
critical_gotchas:
  - id: G1
    title: UseWindowsForms type ambiguity
    rule: >
      UseWindowsForms=true pulls System.Windows.Forms into scope. Always fully-qualify:
      System.Windows.Controls.UserControl, System.Windows.MessageBox.Show(),
      System.Windows.Media.Color (not System.Drawing.Color),
      Microsoft.Win32.OpenFileDialog (not System.Windows.Forms.OpenFileDialog),
      System.Windows.Input.KeyEventArgs (not System.Windows.Forms.KeyEventArgs),
      System.Windows.Controls.MediaElement (not any Forms equivalent)

  - id: G2
    title: EF Core Design on startup project
    rule: Microsoft.EntityFrameworkCore.Design must be in BOTH Infrastructure.csproj AND WPF.csproj. PrivateAssets=all prevents transitive flow.

  - id: G3
    title: Never DateTime.UtcNow in seed data
    rule: "Use: static readonly DateTime SeedDate = new(2025,1,1,0,0,0,DateTimeKind.Utc)"

  - id: G4
    title: IsBusy guard in LoadAsync
    rule: LoadAsync() opens with "if (IsBusy) return;". NEVER set IsBusy=true before calling LoadAsync. LoadAsync owns its own busy state.

  - id: G5
    title: ProjectionService subscriber safety
    rule: Subscriber exceptions caught+logged — never propagate. If projection window stops updating, check log for [ERR].

  - id: G6
    title: SongRepository.UpdateAsync replaces ALL sections
    rule: Load existing tracked → RemoveRange(existing.Sections) → add incoming with Id=0. Never reuse old section Ids.

  - id: G7
    title: Bible import batch pattern
    rule: ImportVersionAsync inserts in batches of 1000 + ChangeTracker.Clear(). DO NOT remove — full Bible ≈ 31,000 verses.

  - id: G8
    title: DataTemplate required for every navigated ViewModel
    rule: ContentControl resolves views via DataTemplate in App.xaml. Missing DataTemplate → blank content, no error, very confusing.

  - id: G9
    title: IProjectionService event cleanup in every subscriber
    rule: Every class subscribing to IProjectionService events must unsubscribe in OnClosed() or Dispose(). Without this → GC blocked + dead Dispatcher. Applies to ProjectionWindow, StageViewModel, ServiceScheduleViewModel.

  - id: G10
    title: WPF dark-theme ComboBoxItem visibility
    rule: FieldComboBox has full ControlTemplate on ItemContainerStyle. Do NOT simplify — without it, dropdown items are invisible on dark theme. Also use ItemTemplate (not DisplayMemberPath) with custom ControlTemplate.

  - id: G11
    title: Scope-per-navigation is mandatory
    rule: MainViewModel._currentScope disposed+replaced on every NavigateTo<T>(). Always resolve page VMs via NavigateTo<T>().

  - id: G12
    title: CommunityToolkit classes must be partial
    rule: "[ObservableProperty], [RelayCommand], [NotifyCanExecuteChangedFor], [NotifyPropertyChangedFor] require source generation. Class AND all containing classes must be partial."

  - id: G13
    title: XAML CommandParameter must be string for enum parsing
    rule: Buttons pass CommandParameter="Verse" (enum name as string). Parse via Enum.TryParse<SectionType>. Never use int or enum values directly in XAML CommandParameter.

  - id: G14
    title: Testament enum values are Old/New
    rule: "OpenAdoration.Domain.Enums.Testament: exactly two values Old and New. Testament.OldTestament → CS0117 compile error."

  - id: G15
    title: CollectionViewSource SortDescription requires scm namespace
    rule: "xmlns:scm=\"clr-namespace:System.ComponentModel;assembly=WindowsBase\" — used in BibleView.xaml for book sort."

  - id: G16
    title: BibleViewModel.SelectedChapter uses 0 as sentinel
    rule: SelectedChapter is int (not int?). Collection is 1..N. 0 deselects cleanly. Guard in OnSelectedChapterChanged: if (value > 0) LoadVersesAsync.

  - id: G17
    title: AlignToggleButton IsChecked must be Mode=OneWay
    rule: TwoWay would overwrite VM property with bool instead of firing SetAlignmentCommand — binding type mismatch.

  - id: G18
    title: Run.Text bindings on read-only properties must be Mode=OneWay
    rule: "<Run Text=\"{Binding SomeProp, Mode=OneWay}\"/> — Run.Text defaults to TwoWay; source-generated int/count properties are read-only → runtime BindingExpression error."

  - id: G19
    title: DispatcherTimer must be stopped on every exit path
    rule: >
      DispatcherTimer holds a strong reference and fires even after the owning VM is navigated away from.
      Stop it in StopLive(), OnProjectionStateChanged(false=stopped), AND Dispose().
      Pattern: always one-shot (stop before advancing; SlideChanged restarts it for the next slide).

  - id: G20
    title: Token zone auto-hide uses letter/digit check, not whitespace trim
    rule: >
      After resolving a header/footer template, show the zone only if resolved.Any(char.IsLetterOrDigit).
      Whitespace trim alone misses "  :" produced by "[BibleChapterID]:[BibleVerseID]" on a song slide.
      Static text always passes; pure-token templates on wrong slide type collapse cleanly.

# ─────────────────────────────────────────────────────────────────────────────
feature_status:  # as of 2026-05-29
  Songs: DONE — full CRUD + two-step search (title/author + lyrics FTS with prefix matching)
         + projection; section validation; VerseOrder; Copyright; CcliNumber; OpenLyrics import
  Themes: DONE — full CRUD + live preview; BackgroundType(Color/Image/Video); xctk:ColorPicker;
          text alignment; HeaderTemplate/FooterTemplate with token chips; 3-zone projection
  Bible: DONE — 3-column browser; single-click projection; FTS search; 8-format import (8/8 tests);
         localized book names; [BibleReference] token ("John 3:16"); cancel+summary
  ServiceSchedule: DONE — service list + builder (song/bible/media, reorder ▲▼, auto-advance [⏱])
                   + live mode (per-item projection, Prev/Next item, click-to-jump, auto-advance timer)
  Media: DONE — import, project, delete; ProjectionWindow handles SlideType.Media (image + video with audio)
  Projection: DONE — 3-zone layout (Header/Body/Footer); ITokenResolver; theme per slide via IServiceScopeFactory;
              CornerLabel fallback; Open/Close screen toggle; full event bus for stage coordination
  StageView: DONE — embedded nav section; themed 1920×1080 Viewbox previews; cross-item UP NEXT;
             Prev/Next Item buttons (visible only when IsServiceScheduleActive); real video via MediaElement
  TokenSystem: DONE — 10 tokens across song+bible; auto-hide zones; clickable chip insertion in theme editor
  AutoAdvance: DONE — per-item seconds (0=manual); DispatcherTimer resets on every SlideChanged;
               persists to DB immediately; cross-item advance at last slide

# ─────────────────────────────────────────────────────────────────────────────
confirmed_bugs:  # all FIXED
  B1: Slide.Blank() threw ArgumentException — content check now exempts SlideType.Blank
  B2: ProjectionWindow hardcoded font — now reads ThemeId via IServiceScopeFactory → IThemeService
  B3: RESOLVED_BY_FRAMEWORK — AsyncRelayCommand handles async exceptions
  B4: Root container captured scoped services — fixed by scope-per-navigation (G11)
  B5: SongService.GenerateSlides threw on empty-lyrics sections — filter with Where()
  B6: ProjectionWindow auto-opened on launch — hidden at startup; EnsureShown() on first projection
  B7: Song title/section label missing on projection — CornerLabel overlay (top-left ZIndex=100)
  B8: Classification dropped on song edit — added existing.Classification = song.Classification
  B9: HeaderTemplate/FooterTemplate silently discarded on theme save — ThemeRepository.UpdateAsync missing two field assignments
  B10: Lyrics FTS "cura" not matching "curará" — changed from quoted phrase to prefix* matching per word
  B11: TaskCanceledException noise in search debounce — replaced Task.Delay(ct) with Task.Delay + ct check

# ─────────────────────────────────────────────────────────────────────────────
roadmap:
  # Original milestones (all DONE)
  milestone_0: Songs CRUD + projection — DONE
  milestone_0b: Projection UX (hidden window, corner label, Open/Close toggle) — DONE
  milestone_1: Themes (CRUD + BackgroundType + ColorPicker + text alignment) — DONE
  milestone_2: Bible Browser (3-column, FTS search, 8-format import, 8/8 tests) — DONE
  milestone_3: Service Schedule (list + builder + live mode) — DONE
  milestone_4: Media (import, project, delete) — DONE
  milestone_5: Keyboard Shortcuts (Space/arrows/B/Esc/1-9/Ctrl+1-5) — DONE
  milestone_6: Polish & Release — IN PROGRESS

  # VP-parity work (all DONE)
  vp_tier1: VerseOrder on Song; SongSectionsFts; ITokenResolver; OpenLyrics import — DONE
  vp_tier2: 3-zone projection layout (Header/Body/Footer) with token rendering — DONE
  vp_stage: Stage View embedded nav; themed previews; cross-item UP NEXT; video; Prev/Next Item — DONE
  vp_tokens_extra: [BibleReference]; Copyright+CcliNumber fields; [SongCopyright][SongCCLI] tokens — DONE
  vp_autoadvance: Auto-advance per schedule item (DispatcherTimer, +/- UI, DB persist) — DONE

  # Remaining P1 (VP parity gap matrix)
  next_p1_verse_order_override:
    title: Verse order override per agenda item
    description: >
      Let a SongScheduleItem specify its own section order for one service,
      overriding Song.VerseOrder. Add VerseOrderOverride (NULL TEXT) to SongScheduleItem.
      UI: editable token string in the builder row or live mode panel.
      Logic: ServiceScheduleViewModel passes override to SongService.GenerateSlides() if set.
    migration: AddSongScheduleItemVerseOrderOverride

  next_p1_settings:
    title: Settings page + church CCLI [SiteLicense] token
    description: >
      Persistent app settings: ChurchName, ChurchCcliNumber, DefaultAutoAdvanceSeconds.
      Stored in %LOCALAPPDATA%\OpenAdoration\settings.json (not DB — no migration needed).
      [SiteLicense] token → resolves to ChurchCcliNumber; [ChurchName] → church name.
      New SettingsViewModel + SettingsView nav section; IAppSettingsService (singleton).

  # P2 remaining
  p2_live_announcement: Push free-text slide to screen mid-service without stopping projection
  p2_bible_phrase_search: Exact phrase mode alongside FTS keyword search (FTS5 "..." syntax)
  p2_additional_song_imports: OpenSong, plain text

# ─────────────────────────────────────────────────────────────────────────────
common_operations:
  build: "dotnet build --configuration Debug"
  run: "dotnet run --project OpenAdoration.WPF"
  test: "dotnet test OpenAdoration.Tests.Infrastructure"
  add_migration: "dotnet ef migrations add <Name> --project OpenAdoration.Infrastructure --startup-project OpenAdoration.WPF"
  reset_db: 'Remove-Item "$env:LOCALAPPDATA\OpenAdoration\openadoration.db" -Force'
  view_logs: 'Get-Content "$env:LOCALAPPDATA\OpenAdoration\logs\openadoration-$(Get-Date -Format yyyyMMdd).log" -Tail 100 -Wait'
  add_new_feature_checklist:
    - "Application: interface + service"
    - "Infrastructure: repo + entity configuration"
    - "WPF: ViewModel (replace stub) + View (replace stub)"
    - "App.xaml: DataTemplate entry — verify class name matches (G8)"
    - "Migration: only if schema changes"
    - "If IProjectionService changes: add to interface, implement in ProjectionService, update Stop() clearing logic"

# ─────────────────────────────────────────────────────────────────────────────
patterns:
  viewmodel_load_pattern: |
    // code-behind Loaded event:
    if (DataContext is MyViewModel vm && vm.LoadCommand.CanExecute(null))
        vm.LoadCommand.Execute(null);

  isBusy_guard_pattern: |
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true; ClearError();
        try { /* ... */ }
        catch (Exception ex) { _logger.LogError(ex, "..."); SetError("..."); }
        finally { IsBusy = false; }
    }

  section_vm_events_pattern: |
    private void SubscribeSectionEvents(SongSectionViewModel vm)
    {
        vm.MoveUpRequested += OnMoveUp; vm.MoveDownRequested += OnMoveDown; vm.DeleteRequested += OnDelete;
    }
    // Always unsubscribe in OnDelete and in Dispose(). Prevents memory leaks.

  relaycommand_commandparameter_string: |
    // XAML: <Button Command="{Binding AddSectionCommand}" CommandParameter="Verse" />
    [RelayCommand]
    private void AddSection(string? sectionTypeName) { Enum.TryParse<SectionType>(sectionTypeName, out var type); ... }

  relativesource_inner_template: |
    Command="{Binding DataContext.SomeCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
    CommandParameter="{Binding}"

  bible_chapter_sentinel: |
    partial void OnSelectedChapterChanged(int value)
    {
        DisplayedVerses.Clear();
        if (value > 0 && SelectedVersion is not null && SelectedBook is not null)
            _ = LoadVersesAsync(SelectedVersion.Id, SelectedBook.Name, value);
    }

  text_alignment_vm_pattern: |
    [ObservableProperty] private System.Windows.TextAlignment _textAlignment = System.Windows.TextAlignment.Center;
    public bool IsAlignLeft   => TextAlignment == System.Windows.TextAlignment.Left;
    public bool IsAlignCenter => TextAlignment == System.Windows.TextAlignment.Center;
    public bool IsAlignRight  => TextAlignment == System.Windows.TextAlignment.Right;
    partial void OnTextAlignmentChanged(System.Windows.TextAlignment _)
    { OnPropertyChanged(nameof(IsAlignLeft)); OnPropertyChanged(nameof(IsAlignCenter)); OnPropertyChanged(nameof(IsAlignRight)); }
    [RelayCommand]
    private void SetAlignment(string alignment) =>
        TextAlignment = alignment switch { "Left" => ..., "Right" => ..., _ => System.Windows.TextAlignment.Center };
    // XAML: IsChecked="{Binding IsAlignCenter, Mode=OneWay}" (Mode=OneWay mandatory — G17)

  auto_advance_timer_pattern: |
    // DispatcherTimer — always one-shot (stopped before advancing; SlideChanged restarts).
    private DispatcherTimer? _autoAdvanceTimer;
    private void StartAutoAdvanceTimer(int seconds) {
        StopAutoAdvanceTimer();
        _autoAdvanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _autoAdvanceTimer.Tick += OnAutoAdvanceTick;
        _autoAdvanceTimer.Start();
    }
    private void StopAutoAdvanceTimer() {
        if (_autoAdvanceTimer is null) return;
        _autoAdvanceTimer.Stop();
        _autoAdvanceTimer.Tick -= OnAutoAdvanceTick;
        _autoAdvanceTimer = null;
    }
    // Stop in StopLive(), OnProjectionStateChanged(false), and Dispose() — G19.

  slide_preview_record_pattern: |
    // Immutable record for StageViewModel → swapping whole object triggers re-eval of all bindings.
    public sealed record SlidePreview { ... init-only properties ... }
    [ObservableProperty] private SlidePreview _currentPreview = SlidePreview.Empty;
    // View binds: {Binding CurrentPreview.FontFamily}, {Binding CurrentPreview.BgColor, Converter=ColorToBrush}
    // WPF uses TypeConverter for string→FontFamily; Color→Brush needs ColorToBrush converter.

  token_zone_visibility_pattern: |
    // After resolving a header/footer template, only show zone if result has alphanumeric content.
    var resolved = _tokenResolver.Resolve(template, context);
    if (resolved.Any(char.IsLetterOrDigit)) {
        HeaderText.Text = resolved;
        HeaderText.Visibility = Visibility.Visible;
    }
    // G20: whitespace trim alone is not enough — "  :" passes trim but has no useful content.
