# OpenAdoration — Claude Context File
# Last updated: 2026-05-20
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
    Application: OpenAdoration.Application — Service+Repo interfaces, Slide DTO, SlideType
    Infrastructure: OpenAdoration.Infrastructure — EF Core DbContext, repos, migrations, logging
    WPF: OpenAdoration.WPF — App/Windows, ViewModels, Views, Converters, Helpers, Styles

  navigation_pattern: >
    ContentControl Content="{Binding CurrentView}". App.xaml maps ViewModel → View UserControl.
    MainViewModel.NavigateTo<T>(): creates IServiceScope, resolves VM, assigns CurrentView, disposes old scope.
    Views fire LoadCommand from Loaded event in code-behind.

  di_lifetime_rules:
    singletons: MainViewModel, MainWindow, ProjectionWindow, IProjectionService
    transients: SongsViewModel, BibleViewModel, ServiceScheduleViewModel, MediaViewModel, ThemeViewModel
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
  current_migration: 20260520041713_AddBibleVersesFts
  migrations_history:
    - 20260505012006_InitialCreate
    - 20260511000000_AddSongClassification
    - 20260518_AddThemeVideoBackground
    - 20260519005541_AddThemeTextAlignment
    - 20260520041713_AddBibleVersesFts

  tables:
    Songs: Id, Title(NOT NULL), Author(NULL), Classification(NULL), CreatedAt, UpdatedAt
    SongSections: Id, SongId(FK→Songs CASCADE), Type(INT), SectionNumber, Lyrics(NOT NULL), Order(NOT NULL), CreatedAt, UpdatedAt
    Themes: Id, Name, FontFamily, FontSize, FontColor, BackgroundColor, BackgroundImagePath(NULL), BackgroundVideoPath(NULL), TextAlignment(TEXT default "Center"), IsDefault, CreatedAt, UpdatedAt
      seed: Id=1 Arial 48pt white/black IsDefault=true — STATIC date new DateTime(2025,1,1,0,0,0,Utc) never DateTime.UtcNow (G3)
    BibleVersions: Id, Name, Abbreviation, Language, CreatedAt, UpdatedAt
    BibleBooks: Id, BibleVersionId(FK), Name, Abbreviation, Testament(INT), BookNumber, ChapterCount, CreatedAt, UpdatedAt
    BibleVerses: Id, BibleVersionId, Book, Chapter, Verse, Text, CreatedAt, UpdatedAt
    BibleVersesFts: FTS5 virtual table — Text indexed, BibleVersionId UNINDEXED; rowid=BibleVerses.Id; tokenize=unicode61
    WorshipServices: Id, Name, Date, CreatedAt, UpdatedAt
    ScheduleItems: TPH discriminator ItemType("Song"|"Bible"|"Media"); (ServiceId,Order) composite index
      song_extra: SongId(FK); bible_extra: Book,Chapter,VerseStart,VerseEnd,BibleVersionId(NULL FK); media_extra: MediaFileId(FK)
    MediaFiles: Id, FileName, FilePath, Type(INT: Image=0 Video=1), CreatedAt, UpdatedAt

  key_patterns:
    timestamp: StampTimestamps() — Added sets CreatedAt+UpdatedAt; Modified sets only UpdatedAt.
    update_song: Load existing tracked → RemoveRange(sections) → add incoming Id=0 (forces INSERT). Never reuse Ids.
    bible_import: Batch 1000 rows + ChangeTracker.Clear() per batch. Required — full Bible ≈ 31,000 verses.

# ─────────────────────────────────────────────────────────────────────────────
projection_engine:
  class: ProjectionService (IProjectionService, Singleton)
  api:
    LoadSlides(slides, contextLabel): starts projection; fires SlideChanged(slides[0]) + ProjectionStateChanged(true)
    Next/Previous/GoTo(index): advances/decrements/jumps; fires SlideChanged
    ShowBlank(): fires SlideChanged(Slide.Blank()) without stopping
    Stop(): clears; fires SlideChanged(null) + ProjectionStateChanged(false)
  events: SlideChanged(Slide?), ProjectionStateChanged(bool)
  safety: Each subscriber wrapped in try/catch; crash never stops engine.

  slide_dto: Content(string), Type(SlideType), Label(string), MediaPath(string?), ThemeId(int?)
    Slide.Blank(): factory; constructor exempts Blank+Media from content requirement.
  slide_types: Song, Bible, Media, Blank

  projection_window:
    shows_on: secondary monitor (fallback primary); hidden at startup; shown on first projection or "Open Screen" click
    Song/Bible: TextViewbox + SlideTextBlock; theme applied via IServiceScopeFactory → IThemeService per slide
    Media: BitmapImage → BackgroundImage. Blank: BlankOverlay visible.
    cleanup: OnClosed() unsubscribes both events (G9).

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
    - OpenAdoration.WPF/App.xaml.cs — DI root, host startup, DB init
    - OpenAdoration.WPF/App.xaml — DataTemplate nav map + global converters (ColorToBrush, TestamentToLabel, InverseBoolToVisibility); every navigated VM needs a DataTemplate here (G8)
    - OpenAdoration.WPF/MainWindow.xaml — shell: 200px sidebar + ContentControl + bottom projection bar
    - OpenAdoration.WPF/ProjectionWindow.xaml.cs  ★ — RenderSlide dispatch, theme via IServiceScopeFactory, Dispatcher.Invoke
    - OpenAdoration.WPF/ViewModels/BaseViewModel.cs — IsBusy, ErrorMessage, SetError(), ClearError()
    - OpenAdoration.WPF/ViewModels/MainViewModel.cs  ★ — scope-per-nav, projection controls
    - OpenAdoration.WPF/ViewModels/SongsViewModel.cs  ★ — full songs CRUD + search + projection
    - OpenAdoration.WPF/ViewModels/BibleViewModel.cs  ★ — cascade; import; SelectedChapter=0 sentinel (G16)
    - OpenAdoration.WPF/ViewModels/AddEditThemeViewModel.cs  ★ — BackgroundType enum; TextAlignment as WPF enum; font/color pickers
    - OpenAdoration.WPF/ViewModels/ServiceScheduleViewModel.cs  ★ — service list + builder + live mode; ScheduleItemViewModel
    - OpenAdoration.WPF/Views/BibleView.xaml  ★ — 3-column browser; CollectionViewSource grouped by Testament; chapter WrapPanel
    - OpenAdoration.WPF/Views/ServiceScheduleView.xaml  ★ — 3-panel (list/builder/live); Run.Text bindings need Mode=OneWay (G18)
    - OpenAdoration.WPF/Helpers/BibleImport/BibleFormatDispatcher.cs  ★ — format auto-detection + dispatch
    - OpenAdoration.WPF/Helpers/BibleImport/OsisBookCatalog.cs — 66 canonical books; GetOrFallback; GetByNumber
    - OpenAdoration.WPF/Styles/Base.xaml  ★ — all control styles; FieldComboBox has full ControlTemplate (G10); DatePicker needs full ControlTemplate too
    - OpenAdoration.Tests.Infrastructure/BibleImport/BibleParserTests.cs — 8/8 format tests

  Application:
    - OpenAdoration.Application/Common/Slide.cs  ★ — projection DTO; Blank() factory; content validation
    - OpenAdoration.Application/Services/IProjectionService.cs + ProjectionService.cs  ★
    - OpenAdoration.Application/Services/IBibleService.cs — GetVersionsAsync/GetBooksAsync/GetVersesAsync/SearchAsync/ImportVersionAsync/GenerateSlide/DeleteVersionAsync

  Infrastructure:
    - OpenAdoration.Infrastructure/Persistence/AppDbContext.cs  ★ — StampTimestamps(), ApplyConfigurationsFromAssembly
    - OpenAdoration.Infrastructure/Repositories/SongRepository.cs  ★ — replace-all-sections pattern
    - OpenAdoration.Infrastructure/Repositories/BibleRepository.cs  ★ — batched 1000-row import + FTS sync
    - OpenAdoration.Infrastructure/Configurations/ScheduleItemConfiguration.cs  ★ — TPH discriminator
    - OpenAdoration.Infrastructure/Extensions/InfrastructureServiceExtensions.cs  ★ — AddInfrastructure(), InitialiseDatabaseAsync()

  Domain:
    - OpenAdoration.Domain/Entities/Theme.cs — FontFamily, FontSize, FontColor, BackgroundColor, BackgroundImagePath, BackgroundVideoPath, TextAlignment
    - OpenAdoration.Domain/Entities/BibleBook.cs — Testament enum (Old/New — G14)
    - OpenAdoration.Domain/Enums/SectionType.cs — [Verse, Chorus, PreChorus, Bridge, Intro, Outro, Tag]
    - OpenAdoration.Domain/Enums/Testament.cs — [Old, New]  # CRITICAL: NOT OldTestament/NewTestament

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
      Microsoft.Win32.OpenFileDialog (not System.Windows.Forms.OpenFileDialog)

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
    title: ProjectionWindow event cleanup
    rule: OnClosed() must unsubscribe both IProjectionService events. Without this → GC blocked + dead Dispatcher.

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

# ─────────────────────────────────────────────────────────────────────────────
feature_status:  # as of 2026-05-20
  Songs: DONE — full CRUD + search + projection; section validation on save (non-empty, all have lyrics)
  Themes: DONE — full CRUD + live preview; BackgroundType(Color/Image/Video); xctk:ColorPicker; text alignment; projection applies theme per slide via IServiceScopeFactory
  Bible: DONE — 3-column browser; single-click projection; FTS search; 8-format import (8/8 tests); localized book names; cancel+summary
  ServiceSchedule: DONE — service list + builder (song/bible/media items, reorder ▲▼) + live mode (per-item projection, Prev/Next item, click-to-jump)
  Media: STUB — domain/service/repo done; no import UI yet; ProjectionWindow already handles SlideType.Media
  Projection: DONE — engine + ProjectionWindow; theme per slide; corner label (title+section); preview thumbnail; Open/Close screen toggle

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

# ─────────────────────────────────────────────────────────────────────────────
roadmap:
  milestone_0: Songs CRUD + projection — DONE
  milestone_0b: Projection UX (B6 hidden window, B7 corner label, preview thumbnail, Open/Close toggle) — DONE
  milestone_1: Themes (CRUD + BackgroundType + ColorPicker + text alignment + projection applies theme) — DONE
  milestone_2: Bible Browser (3-column, click-to-project, FTS search, 5-format import) — DONE
  milestone_2b: Bible importer hardening (cancel, success summary, exception catches, 5 tests) — DONE
  milestone_2c: BibleSuperSearch JSON/ZIP/SQLite parsers; GetByNumber; 8/8 tests — DONE
  milestone_3: Service Schedule (service list + builder + live mode) — DONE

  milestone_4:
    title: Media
    status: NEXT
    key_work:
      - MediaViewModel — LoadCommand, ImportFileCommand (copy-on-import to %LocalAppData%\OpenAdoration\Media\), DeleteFileCommand, ProjectFileCommand
      - MediaView.xaml — WrapPanel of image cards (thumbnail 160×90, filename, Project/Delete), Import toolbar button
      - No new migrations needed — MediaFiles table already exists

  milestone_5:
    title: Keyboard Shortcuts
    status: PLANNED
    key_work: KeyDown handler in MainWindow.xaml.cs (not XAML bindings)
    shortcuts: "Space/→/PageDown=Next, ←/PageUp=Previous, B=Blank, Esc=Stop, 1-9=GoTo(N), Ctrl+1/2/3=navigate tabs"

  milestone_6:
    title: Polish & Release
    status: PLANNED

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
    - "App.xaml: DataTemplate already registered — verify class name matches"
    - "Migration: only if schema changes"

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
    // Always unsubscribe in OnDelete and OnEditCancelled to prevent leaks.

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
        TextAlignment = alignment switch { "Left" => System.Windows.TextAlignment.Left, "Right" => System.Windows.TextAlignment.Right, _ => System.Windows.TextAlignment.Center };
    // XAML: IsChecked="{Binding IsAlignCenter, Mode=OneWay}" (Mode=OneWay mandatory — G17)
