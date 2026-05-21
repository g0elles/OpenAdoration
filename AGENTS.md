# OpenAdoration — Codex Context File
# Last updated: 2026-05-18
# Purpose: Give Codex full project context in one read so no files need
#          to be re-read to understand structure, patterns, or current state.
# Format: YAML — read this before touching any code in this repo.

---

# ─────────────────────────────────────────────────────────────────────────────
code_standards:
  status: MANDATORY — non-negotiable on every change, no exceptions.

  principles:
    SOLID:
      S: >
        Single Responsibility — every class/method does one thing.
        ViewModels hold state and commands only; services hold business logic only;
        repositories hold data access only. Never mix these responsibilities.
      O: >
        Open/Closed — extend via new classes or interfaces, not by modifying
        existing ones. New slide types, new services, new pages must slot in
        without touching existing code.
      L: >
        Liskov Substitution — implementations must be fully substitutable for
        their interfaces. No interface method that some implementors must leave
        empty or throw NotImplementedException on.
      I: >
        Interface Segregation — keep interfaces narrow. A caller should never
        depend on methods it does not use. Split fat interfaces before adding them.
      D: >
        Dependency Inversion — depend on abstractions (interfaces), never on
        concrete types across layers. WPF → Application interface only.
        Never reference Infrastructure types from WPF or Application.

    clean_code:
      - No dead code, no commented-out blocks, no TODO/FIXME left in committed code.
      - Names are intention-revealing; no abbreviations except well-known ones (VM, UI, DI).
      - Methods stay small (target < 20 lines); extract named private helpers otherwise.
      - No magic strings or magic numbers — use constants, enums, or named values.
      - Validate at system boundaries (user input, external APIs); trust internals.
      - One level of abstraction per method — no mixing high-level orchestration with
        low-level implementation detail in the same function body.

    reliability:
      - All IDisposable classes implement Dispose() and unsubscribe events.
      - Event subscriptions on long-lived objects always have a matching unsubscription.
      - UI property updates from non-UI-thread callers must go through Dispatcher.Invoke.
      - CancellationToken propagated through all async chains (service → repository).
      - EF Core SaveChangesAsync is implicitly transactional; never call it twice for
        one logical operation — batch all changes into a single call.

    patterns_enforced:
      - Scope-per-navigation (see G11) — never resolve page VMs from the root container.
      - IsBusy guard (see G4) — LoadAsync owns its own busy state, callers do not set it.
      - Dispatcher.Invoke in MainViewModel and any singleton that subscribes to
        IProjectionService events (fire can come from any thread in the future).
      - IDisposable on every ViewModel that subscribes to external events.

  what_not_to_do:
    - Do not add error handling for impossible code paths (trust the framework).
    - Do not add backwards-compatibility shims for code you are changing.
    - Do not design for hypothetical future requirements beyond the current milestone.
    - Do not inject concrete classes across layer boundaries — always use interfaces.
    - Do not leave stub ViewModels with unused injected services; trim to what is used.

---

project:
  name: OpenAdoration
  description: >
    Free, open-source Windows desktop worship-presentation app
    (EasyWorship alternative). One operator runs it during a church service
    to control what appears on a projector screen. Fully offline — no network
    calls, no accounts, no cloud sync. All data lives in a single SQLite file.
  platform: Windows 10+ only
  target_framework: net10.0-windows
  solution_file: OpenAdoration.sln
  db_path: "%LOCALAPPDATA%\\OpenAdoration\\openadoration.db"
  log_dir: "%LOCALAPPDATA%\\OpenAdoration\\logs"

# ─────────────────────────────────────────────────────────────────────────────
tech_stack:
  ui: WPF (.NET 10)
  orm: Entity Framework Core 9 + SQLite
  mvvm: CommunityToolkit.Mvvm 8.4.0  # source-generated [ObservableProperty], [RelayCommand]
  di: Microsoft.Extensions.Hosting (generic host)
  logging: Serilog (rolling daily file + debug sink)
  multi_monitor: System.Windows.Forms.Screen.AllScreens  # requires UseWindowsForms=true in .csproj

  nuget_packages:
    - Microsoft.Extensions.Hosting: "9.0.4"
    - Microsoft.EntityFrameworkCore: "9.0.4"
    - Microsoft.EntityFrameworkCore.Sqlite: "9.0.4"
    - Microsoft.EntityFrameworkCore.Design: "9.0.4"  # PrivateAssets=all; present in BOTH Infrastructure.csproj AND WPF.csproj
    - CommunityToolkit.Mvvm: "8.4.0"
    - Serilog: "4.2.0"
    - Serilog.Extensions.Logging: "9.0.0"
    - Serilog.Sinks.File: "6.0.0"
    - Serilog.Sinks.Debug: "3.0.0"
    - Extended.Wpf.Toolkit: "5.0.0"  # xctk:ColorPicker used in AddEditThemeView.xaml

# ─────────────────────────────────────────────────────────────────────────────
architecture:
  pattern: Clean Architecture (Domain → Application → Infrastructure ← WPF)
  dependency_rule: >
    WPF references Application only (never Infrastructure directly).
    Infrastructure references Application only.
    Domain references nothing.
    All cross-layer calls go through Application interfaces.

  layers:
    Domain:
      project: OpenAdoration.Domain
      contents: Entities, Enums, BaseEntity (Id/CreatedAt/UpdatedAt)
      references: nothing

    Application:
      project: OpenAdoration.Application
      contents: Service interfaces + implementations, Repository interfaces, Slide DTO, SlideType enum
      references: Domain

    Infrastructure:
      project: OpenAdoration.Infrastructure
      contents: EF Core DbContext, entity configurations, repository implementations, migrations, logging setup
      references: Application

    WPF:
      project: OpenAdoration.WPF
      contents: App/Windows, ViewModels, Views, Converters, Helpers, Styles
      references: Application (interfaces only — never Infrastructure)

  navigation_pattern: >
    ContentControl in MainWindow.xaml has Content="{Binding CurrentView}".
    App.xaml holds DataTemplate mappings: ViewModel type → View UserControl.
    MainViewModel.NavigateTo<T>() creates a new IServiceScope, resolves the VM
    from it, assigns to CurrentView, then disposes the old scope.
    Views fire LoadCommand from their Loaded event in code-behind.

  di_lifetime_rules:
    MainViewModel: Singleton
    MainWindow: Singleton
    ProjectionWindow: Singleton
    IProjectionService / ProjectionService: Singleton
    page_viewmodels: Transient (SongsViewModel, BibleViewModel, etc.)
    services: Scoped (ISongService, IBibleService, IThemeService, IMediaService, IWorshipServiceService)
    repositories: Scoped (ISongRepository, IBibleRepository, etc.)
    IDbContextFactory<AppDbContext>: registered by AddDbContextFactory (Singleton factory, Scoped context)

  scope_per_navigation: >
    CRITICAL: MainViewModel stores _currentScope (IServiceScope?).
    NavigateTo<T>() creates a new scope, resolves T from it, sets CurrentView,
    then disposes the previous scope. This prevents scoped services from being
    captured in the root container. NEVER call GetRequiredService<T>() directly
    on the root IServiceProvider for page-level ViewModels.

# ─────────────────────────────────────────────────────────────────────────────
di_registration:  # App.xaml.cs — RegisterViewModels / RegisterWindows
  singletons:
    - MainViewModel
    - MainWindow
    - ProjectionWindow
  transients:
    - SongsViewModel
    - BibleViewModel
    - ServiceScheduleViewModel
    - MediaViewModel
    - ThemeViewModel
  # Infrastructure registrations (InfrastructureServiceExtensions.AddInfrastructure):
  scoped_services:
    - ISongService → SongService
    - IBibleService → BibleService
    - IThemeService → ThemeService
    - IMediaService → MediaService
    - IWorshipServiceService → WorshipServiceService
  scoped_repositories:
    - ISongRepository → SongRepository
    - IBibleRepository → BibleRepository
    - IThemeRepository → ThemeRepository
    - IMediaRepository → MediaRepository
    - IWorshipServiceRepository → WorshipServiceRepository
  singleton_services:
    - IProjectionService → ProjectionService

# ─────────────────────────────────────────────────────────────────────────────
database:
  engine: SQLite
  migration_command: >
    dotnet ef migrations add <Name>
      --project OpenAdoration.Infrastructure
      --startup-project OpenAdoration.WPF
  migrations_applied: automatically at startup via MigrateAsync() in InitialiseDatabaseAsync()
  current_migration: 20260519005541_AddThemeTextAlignment

  migrations_history:
    - 20260505012006_InitialCreate         # full schema + default theme seed
    - 20260511000000_AddSongClassification  # Classification column on Songs
    - 20260518_AddThemeVideoBackground      # BackgroundVideoPath + BackgroundImagePath on Themes
    - 20260519005541_AddThemeTextAlignment  # TextAlignment column on Themes (default "Center")

  tables:
    Songs:
      columns: Id, Title(NOT NULL), Author(NULL), Classification(NULL), CreatedAt, UpdatedAt
      indexes: [Title]

    SongSections:
      columns: Id, SongId(FK→Songs CASCADE), Type(INT enum), SectionNumber, Lyrics(NOT NULL), Order(NOT NULL), CreatedAt, UpdatedAt

    Themes:
      columns: Id, Name, FontFamily, FontSize, FontColor, BackgroundColor, BackgroundImagePath(NULL), BackgroundVideoPath(NULL), TextAlignment(TEXT default "Center"), IsDefault, CreatedAt, UpdatedAt
      seed: Id=1, Name="Default", Arial 48pt, white text on black bg, IsDefault=true, TextAlignment="Center"
      seed_date: static DateTime(2025,1,1,0,0,0,Utc)  # NEVER use DateTime.UtcNow in seed data

    BibleVersions: Id, Name, Abbreviation, Language, CreatedAt, UpdatedAt
    BibleBooks: Id, BibleVersionId(FK), Name, Abbreviation, Testament(INT), BookNumber, ChapterCount, CreatedAt, UpdatedAt
    BibleVerses: Id, BibleVersionId, Book, Chapter, Verse, Text, CreatedAt, UpdatedAt

    WorshipServices: Id, Name, Date, CreatedAt, UpdatedAt

    ScheduleItems:  # TPH — all subtypes in one table
      discriminator_column: ItemType (TEXT: "Song" | "Bible" | "Media")
      shared_columns: Id, ServiceId(FK→WorshipServices), Order(NOT NULL), ThemeId(NULL FK→Themes SetNull), CreatedAt, UpdatedAt
      song_extra: SongId(FK→Songs)
      bible_extra: Book, Chapter, VerseStart, VerseEnd, BibleVersionId(NULL FK)
      media_extra: MediaFileId(FK→MediaFiles)
      indexes: [(ServiceId, Order) composite]

    MediaFiles: Id, FileName, FilePath, Type(INT: Image=0 Video=1), CreatedAt, UpdatedAt

  timestamp_stamping: >
    AppDbContext.StampTimestamps() runs on every SaveChanges.
    Added → sets CreatedAt + UpdatedAt.
    Modified → sets only UpdatedAt; marks CreatedAt as Unmodified to prevent overwrites.

  update_song_pattern: >
    SongRepository.UpdateAsync loads existing (tracked), calls RemoveRange(existing.Sections),
    then adds incoming sections with Id=0 (forces INSERT). NEVER reuse old section Ids.

  bible_import: >
    BibleRepository.ImportVersionAsync inserts in batches of 1000 + ChangeTracker.Clear()
    after each batch. Required — a full Bible has ~31,000 verses.

# ─────────────────────────────────────────────────────────────────────────────
projection_engine:
  class: ProjectionService (IProjectionService, Singleton)
  state: _slides, _currentIndex, _isProjecting, _contextLabel
  methods:
    LoadSlides(slides, contextLabel): starts projection, fires SlideChanged(slides[0]) + ProjectionStateChanged(true)
    Next(): advances index, fires SlideChanged
    Previous(): decrements index, fires SlideChanged
    GoTo(index): jumps to index, fires SlideChanged
    ShowBlank(): fires SlideChanged(Slide.Blank()) without stopping
    Stop(): clears state, fires SlideChanged(null) + ProjectionStateChanged(false)
  events:
    SlideChanged: EventHandler<Slide?>
    ProjectionStateChanged: EventHandler<bool>
  safety: >
    RaiseSlideChanged and RaiseProjectionStateChanged wrap each subscriber call
    in try/catch and log exceptions. A subscriber crash never stops the engine.

  slide_dto:  # OpenAdoration.Application/Common/Slide.cs
    properties: Content(string), Type(SlideType), Label(string), MediaPath(string?), ThemeId(int?)
    factory: Slide.Blank() → SlideType.Blank, empty Content (valid — constructor exempts Blank from content check)
    validation: content required for all types EXCEPT SlideType.Media and SlideType.Blank

  slide_types:  # OpenAdoration.Application/Common/SlideType.cs
    - Song
    - Bible
    - Media
    - Blank

  projection_window:
    renders_on: secondary monitor (fallback: primary)
    launched_by: MainWindow.OnContentRendered → _projectionWindow.ShowOnSecondaryScreen()
    events: subscribes to IProjectionService.SlideChanged + ProjectionStateChanged
    rendering:
      Song/Bible: ShowText(slide.Content) → TextViewbox visible, SlideTextBlock.Text + TextAlignment set
      Media: ShowMedia(slide.MediaPath) → BitmapImage loaded into BackgroundImage
      Blank: ShowBlankOverlay() → BlankOverlay visible
      Theme: resolved via IServiceScopeFactory → IThemeService per slide; applied to SlideTextBlock
    cleanup: OnClosed unsubscribes both events (prevents GC + dead Dispatcher issues)

# ─────────────────────────────────────────────────────────────────────────────
bible_import:
  description: >
    Multi-format Bible import pipeline. Entry point: BibleFormatDispatcher.Import(filePath).
    Auto-detects format by file extension + content sniffing. Returns BibleImportResult
    (record of Version, Books, Verses) which BibleViewModel passes to IBibleService.ImportVersionAsync.

  supported_formats:
    Zefania_XML:
      root_elements: [XMLBIBLE, ZEFANIA, ZEFANIABIBLE, XMLBIBLELT]
      parser: ZefaniaXmlParser (uses XDocument)
      book_element: BIBLEBOOK (bnumber/bname/bsname attributes)
      chapter_element: CHAPTER (cnumber attribute)
      verse_element: VERS (vnumber attribute)
      skip_elements: [XREF, NOTE, GRAM]
      sources: OpenSong, EasyWorship exports, Zefania Project

    OSIS_XML:
      root_element: osis
      parser: OsisXmlParser (uses XmlReader, streaming)
      verse_styles:
        milestone: "<verse sID='Gen.1.1'/> text <verse eID='Gen.1.1'/>"
        container: "<verse osisID='Gen.1.1'>text</verse>"
      skip_elements: [note, catchWord, rdg, ref, figure]
      book_ids: OSIS standard IDs (Gen, Exod, Matt, etc.) → OsisBookCatalog lookup
      sources: CrossWire SWORD project, Bible.com exports

    USFX_XML:
      root_element: usfx
      parser: UsfxXmlParser (uses XmlReader, streaming, sequential markers)
      verse_start: "<v id='1'/>"
      verse_end: "<ve/>"
      chapter_marker: "<c id='1'/>"
      book_element: "<book id='GEN'>"
      book_name: "<h>" element (first occurrence) or "<toc level='2'>"
      abbreviation: "<toc level='3'>"
      skip_elements: [note, f, ef, fe, x, ex, rq, zw]
      skip_depth_counter: true  # nested skip elements handled via skipDepth int
      book_ids: same as OSIS → OsisBookCatalog lookup
      sources: eBible.org, seven1m/open-bibles repository

    thiagobodruk_JSON:
      root: JSON array OR object with "books" wrapper
      chapter_format: array of arrays of strings (0-indexed verses)
      book_fields: "abbrev" and ("book" or "name")
      version_name: derived from filename (format has no metadata)
      parser: ThiagobodrukJsonParser
      sources: github.com/thiagobodruk/bible, many derivative repos

    OpenAdoration_JSON:
      root: JSON object with "name", "abbreviation", "language", "books" array
      book_format: "{ name, chapters: [{ number, verses: [{ number, text }] }] }"
      parser: OpenADorationJsonParser
      sources: OpenAdoration's own export format

  dispatcher_logic: >
    Extension → .xml → PeekXmlRoot() → route by root element name (case-insensitive).
    Extension → .json → JsonDocument root: Array → thiagobodruk; Object with "books"
      whose chapters are arrays-of-arrays → thiagobodruk; arrays-of-objects → OpenAdoration.
    Unknown extension → sniff first bytes: '<' → try XML; else → try JSON.

  osis_book_catalog: >
    OsisBookCatalog static dictionary: 66 canonical books.
    Key: OSIS/USFX book ID (e.g. "Gen", "GEN", "Matt").
    Value: BookInfo(Name, Abbreviation, Number, Testament).
    GetOrFallback(id, fallbackNumber, fallbackName) — safe for non-standard IDs.
    Testament values: Testament.Old / Testament.New (NOT OldTestament/NewTestament).

  file_dialog_filter: >
    "Bible files|*.xml;*.json|Zefania XML|*.xml|OSIS XML|*.xml|USFX XML|*.xml
     |OpenAdoration JSON|*.json|Array JSON (thiagobodruk format)|*.json|All files|*.*"

# ─────────────────────────────────────────────────────────────────────────────
key_files:
  # ★ = highest-leverage; read these first when debugging

  WPF:
    - path: OpenAdoration.WPF/App.xaml.cs
      role: DI composition root, host startup/shutdown, DB initialisation
    - path: OpenAdoration.WPF/App.xaml
      role: DataTemplate navigation map (ViewModel type → View UserControl); global converter resources
      note: >
        Every ViewModel navigated to via CurrentView MUST have a DataTemplate here.
        Global converters declared here: ColorToBrushConverter (key: ColorToBrush),
        TestamentToLabelConverter (key: TestamentToLabel),
        InverseBoolToVisibilityConverter (key: InverseBoolToVisibility).
    - path: OpenAdoration.WPF/MainWindow.xaml.cs
      role: Sets DataContext=MainViewModel, fires NavigateToSongsCommand on startup, launches ProjectionWindow
    - path: OpenAdoration.WPF/MainWindow.xaml
      role: Shell — 200px sidebar + ContentControl + bottom projection control bar
    - path: OpenAdoration.WPF/ProjectionWindow.xaml.cs  # ★
      role: RenderSlide dispatch, theme application via IServiceScopeFactory, Dispatcher.Invoke, ShowOnSecondaryScreen
    - path: OpenAdoration.WPF/Helpers/ScreenHelper.cs
      role: GetSecondaryScreen(), PositionOnScreen() using System.Windows.Forms.Screen
    - path: OpenAdoration.WPF/ViewModels/BaseViewModel.cs
      role: IsBusy, ErrorMessage, HasError, SetError(), ClearError()
    - path: OpenAdoration.WPF/ViewModels/MainViewModel.cs  # ★
      role: Navigation (scope-per-nav), projection controls, projection state sync
    - path: OpenAdoration.WPF/ViewModels/SongsViewModel.cs  # ★
      role: Full songs CRUD + search + projection; manages IsBusy guard carefully
    - path: OpenAdoration.WPF/ViewModels/AddEditSongViewModel.cs
      role: Section management; Saved/Cancelled events; RecalculateOrder()
    - path: OpenAdoration.WPF/ViewModels/SongSectionViewModel.cs
      role: Per-section VM; Label computed from Type+SectionNumber; fires events to parent
    - path: OpenAdoration.WPF/ViewModels/BibleViewModel.cs  # ★
      role: >
        Bible Browser — version/book/chapter/verse cascade; single-click projection;
        search with ClearSearch restore; multi-format import via BibleFormatDispatcher.
        SelectedChapter uses 0 as "none" sentinel (never in the 1-N collection).
    - path: OpenAdoration.WPF/ViewModels/AddEditThemeViewModel.cs  # ★
      role: >
        Full theme editor — font family/size (±2 step), font/bg color pickers,
        text alignment (Left/Center/Right toggle), image/video bg pickers, live preview.
        TextAlignment stored as System.Windows.TextAlignment enum; serialised to string at DB boundary.
    - path: OpenAdoration.WPF/Views/SongsView.xaml
      role: Two-panel layout (list / edit), RelativeSource bindings for song row commands
    - path: OpenAdoration.WPF/Views/SongsView.xaml.cs
      role: Triggers LoadCommand on Loaded event
    - path: OpenAdoration.WPF/Views/AddEditSongView.xaml
      role: Title/Author fields + section cards + per-type add buttons (WrapPanel, not ComboBox)
    - path: OpenAdoration.WPF/Views/BibleView.xaml  # ★
      role: >
        3-column browser (Books | Chapters | Verses) + toolbar (version ComboBox, import, delete).
        Books grouped by Testament via CollectionViewSource + PropertyGroupDescription.
        Chapters rendered as 38×38 square tile grid via WrapPanel ItemsPanel.
        Verse row: verse number left column + text right column.
        Search bar replaces verse list when active.
    - path: OpenAdoration.WPF/Views/BibleView.xaml.cs
      role: Fires LoadCommand on Loaded; handles OpenFileDialog → ImportVersionCommand
    - path: OpenAdoration.WPF/Views/AddEditThemeView.xaml
      role: >
        Theme editor form with live preview rectangle.
        xctk:ColorPicker for FontColor and BackgroundColor.
        ± buttons for FontSize (DecreaseFontSizeCommand / IncreaseFontSizeCommand).
        AlignToggleButton strip (Left / Center / Right).
    - path: OpenAdoration.WPF/Helpers/BibleImport/BibleFormatDispatcher.cs  # ★
      role: Auto-detects Bible file format and dispatches to the correct parser
    - path: OpenAdoration.WPF/Helpers/BibleImport/BibleImportResult.cs
      role: Sealed record — (BibleVersion Version, List<BibleBook> Books, List<BibleVerse> Verses)
    - path: OpenAdoration.WPF/Helpers/BibleImport/OsisBookCatalog.cs
      role: Static dictionary of all 66 canonical books; OSIS/USFX ID → canonical name/number/testament
    - path: OpenAdoration.WPF/Helpers/BibleImport/ZefaniaXmlParser.cs
      role: Parses Zefania XML format using XDocument
    - path: OpenAdoration.WPF/Helpers/BibleImport/OsisXmlParser.cs
      role: Parses OSIS XML (milestone and container verse styles) using XmlReader
    - path: OpenAdoration.WPF/Helpers/BibleImport/UsfxXmlParser.cs
      role: Parses USFX XML sequential marker format using XmlReader
    - path: OpenAdoration.WPF/Helpers/BibleImport/ThiagobodrukJsonParser.cs
      role: Parses thiagobodruk array-JSON format (root array or {books:[...]} wrapper)
    - path: OpenAdoration.WPF/Helpers/BibleImport/OpenADorationJsonParser.cs
      role: Parses OpenAdoration native JSON format
    - path: OpenAdoration.WPF/Converters/ColorToBrushConverter.cs
      role: System.Windows.Media.Color → SolidColorBrush (used in theme preview)
    - path: OpenAdoration.WPF/Converters/TestamentToLabelConverter.cs
      role: Testament enum → "OLD TESTAMENT" / "NEW TESTAMENT" string (used in BibleView group headers)
    - path: OpenAdoration.WPF/Converters/InverseBoolToVisibilityConverter.cs
      role: bool→Visibility inverted (true→Collapsed); used for list/edit panel toggle
    - path: OpenAdoration.WPF/Styles/Colors.xaml
      role: All color/brush resources (dark purple theme)
    - path: OpenAdoration.WPF/Styles/Base.xaml  # ★
      role: All control styles — Button variants, TextBox variants, ComboBox, Cards, AlignToggleButton

  Application:
    - path: OpenAdoration.Application/Common/Slide.cs  # ★
      role: Runtime projection unit; constructor validates content; Blank() factory
    - path: OpenAdoration.Application/Common/SlideType.cs
      role: Enum — Song, Bible, Media, Blank
    - path: OpenAdoration.Application/Services/IProjectionService.cs  # ★
    - path: OpenAdoration.Application/Services/ProjectionService.cs  # ★
    - path: OpenAdoration.Application/Services/ISongService.cs
    - path: OpenAdoration.Application/Services/SongService.cs
      note: GenerateSlides filters out empty-lyrics sections (logs warning for skipped)
    - path: OpenAdoration.Application/Services/IBibleService.cs
      note: GetVersionsAsync, GetBooksAsync, GetVersesAsync, SearchAsync, ImportVersionAsync, GenerateSlide, DeleteVersionAsync

  Infrastructure:
    - path: OpenAdoration.Infrastructure/Persistence/AppDbContext.cs  # ★
      role: StampTimestamps(), ApplyConfigurationsFromAssembly
    - path: OpenAdoration.Infrastructure/Repositories/SongRepository.cs  # ★
      role: UpdateAsync replace-all-sections pattern
    - path: OpenAdoration.Infrastructure/Repositories/BibleRepository.cs  # ★
      role: ImportVersionAsync batched 1000-row insert
    - path: OpenAdoration.Infrastructure/Configurations/ScheduleItemConfiguration.cs  # ★
      role: TPH discriminator setup
    - path: OpenAdoration.Infrastructure/Configurations/ThemeConfiguration.cs
      role: Seeds default theme with STATIC date; configures BackgroundImagePath, BackgroundVideoPath, TextAlignment columns
    - path: OpenAdoration.Infrastructure/Extensions/InfrastructureServiceExtensions.cs  # ★
      role: AddInfrastructure(), InitialiseDatabaseAsync()
    - path: OpenAdoration.Infrastructure/Logging/LoggingConfiguration.cs
      role: Configure(), UseOpenAdorationSerilog(), CloseAndFlush()
    - path: OpenAdoration.Infrastructure/Migrations/20260505012006_InitialCreate.cs
      role: Full schema + default theme seed; auto-applied at startup
    - path: OpenAdoration.Infrastructure/Migrations/20260519005541_AddThemeTextAlignment.cs
      role: Adds TextAlignment TEXT column to Themes (default "Center"); updates seed row

  Domain:
    - path: OpenAdoration.Domain/Entities/Song.cs
    - path: OpenAdoration.Domain/Entities/SongSection.cs
      note: Label property computed from Type+SectionNumber
    - path: OpenAdoration.Domain/Entities/Theme.cs
      note: FontFamily, FontSize, FontColor, BackgroundColor, BackgroundImagePath, BackgroundVideoPath, TextAlignment
    - path: OpenAdoration.Domain/Entities/BibleBook.cs
      note: Testament property is Testament enum (values Old / New — NOT OldTestament/NewTestament)
    - path: OpenAdoration.Domain/Enums/SectionType.cs
      values: [Verse, Chorus, PreChorus, Bridge, Intro, Outro, Tag]
    - path: OpenAdoration.Domain/Enums/Testament.cs
      values: [Old, New]  # CRITICAL: NOT OldTestament/NewTestament

# ─────────────────────────────────────────────────────────────────────────────
styles:  # OpenAdoration.WPF/Styles/
  colors:  # Colors.xaml
    PrimaryColor: "#1E1E2E"
    SurfaceColor: "#2A2A3E"
    AccentColor: "#7C6AF7"
    AccentHoverColor: "#9B8FFF"
    DangerColor: "#E05252"
    TextPrimaryColor: "#FFFFFF"
    TextSecondaryColor: "#A0A0B8"
    BorderColor: "#3A3A52"
    ActiveNavColor: "#3A3A5C"

  control_styles:  # Base.xaml — key style keys
    buttons:
      - PrimaryButton      # accent fill
      - SecondaryButton    # outlined, no fill
      - DangerButton       # red fill, BasedOn PrimaryButton
      - IconButton         # small, transparent, for ▲▼ glyphs
      - IconDangerButton   # small, red tint, BasedOn IconButton
      - NavButton          # sidebar navigation
      - ProjectionButton   # bottom projection bar
    textboxes:
      - FieldTextBox       # standard form input with placeholder support
      - SearchTextBox      # search input with placeholder
      - LyricsTextBox      # multiline lyrics area
    combobox:
      - FieldComboBox      # includes full ComboBoxItem ControlTemplate (fixes dark-theme invisible items)
    cards:
      - SongRowCard        # song list item border
      - SectionCard        # section card in AddEditSongView
    labels:
      - FieldLabel         # form field label
    togglebuttons:
      - AlignToggleButton  # text alignment strip (Left/Center/Right); accent bg when IsChecked=True

  placeholder_pattern: >
    DarkTextBoxTemplate ControlTemplate: TextBlock with Visibility="Collapsed" by default.
    MultiTrigger(Text="" AND IsFocused=False) → Visibility=Visible.
    Tag property holds the placeholder string.

# ─────────────────────────────────────────────────────────────────────────────
critical_gotchas:
  - id: G1
    title: UseWindowsForms type ambiguity
    rule: >
      UseWindowsForms=true pulls System.Windows.Forms into scope.
      Always fully-qualify in code-behind:
        public partial class MyView : System.Windows.Controls.UserControl
        System.Windows.MessageBox.Show(...)
        using WpfApp = System.Windows.Application;
        System.Windows.Media.Color (not System.Drawing.Color)
        Microsoft.Win32.OpenFileDialog (not System.Windows.Forms.OpenFileDialog)

  - id: G2
    title: EF Core Design on startup project
    rule: >
      Microsoft.EntityFrameworkCore.Design must be in BOTH
      OpenAdoration.Infrastructure.csproj AND OpenAdoration.WPF.csproj.
      Infrastructure has PrivateAssets=all so it does NOT flow transitively.

  - id: G3
    title: Never DateTime.UtcNow in seed data
    rule: >
      HasData() seed with runtime timestamps generate spurious migrations.
      Use: static readonly DateTime SeedDate = new(2025,1,1,0,0,0,DateTimeKind.Utc)

  - id: G4
    title: IsBusy guard in LoadAsync
    rule: >
      LoadAsync() opens with: if (IsBusy) return;
      NEVER set IsBusy=true before calling LoadAsync from a caller.
      Let LoadAsync own its own busy state entirely.

  - id: G5
    title: ProjectionService subscriber safety
    rule: >
      Subscriber exceptions are caught and logged by the engine — they do NOT propagate.
      If the projection window stops updating, check the log for [ERR] from the subscriber.

  - id: G6
    title: SongRepository.UpdateAsync replaces ALL sections
    rule: >
      Pattern: load existing (tracked) → RemoveRange(existing.Sections) →
      add incoming sections with Id=0 (forces INSERT).
      Never reuse old section Ids — causes duplicate key or concurrency errors.

  - id: G7
    title: Bible import batch pattern
    rule: >
      ImportVersionAsync inserts in batches of 1000 + ChangeTracker.Clear() per batch.
      DO NOT remove this — full Bible ≈ 31,000 verses would exhaust memory without it.

  - id: G8
    title: DataTemplate required for every navigated ViewModel
    rule: >
      ContentControl resolves views via DataTemplate lookup in App.xaml.
      If a ViewModel has no DataTemplate → blank content, no error, very confusing.
      Every Navigate* target must have a DataTemplate in App.xaml.

  - id: G9
    title: ProjectionWindow event cleanup
    rule: >
      OnClosed() unsubscribes both IProjectionService events.
      Without this: Singleton holds reference to closed window → GC blocked + dead Dispatcher.

  - id: G10
    title: WPF dark-theme ComboBoxItem visibility
    rule: >
      Default ComboBoxItem template uses SystemColors.HighlightBrush — ignores all style setters.
      FieldComboBox in Base.xaml has a full ControlTemplate on ItemContainerStyle.
      Do NOT simplify it — without the ControlTemplate, dropdown items are invisible.

  - id: G11
    title: Scope-per-navigation is mandatory
    rule: >
      MainViewModel._currentScope is disposed and replaced on every NavigateTo<T>() call.
      Scoped services (ISongService, DbContext, etc.) must never be captured in the root container.
      Always resolve page VMs via NavigateTo<T>(), never via _services.GetRequiredService<T>() directly.

  - id: G12
    title: CommunityToolkit classes must be partial
    rule: >
      [ObservableProperty], [RelayCommand], [NotifyCanExecuteChangedFor],
      [NotifyPropertyChangedFor] all require source generation.
      The class AND all containing classes must be declared partial.

  - id: G13
    title: AddSectionCommand uses string CommandParameter
    rule: >
      Each "Add X" button passes CommandParameter="Verse" (the SectionType enum name as string).
      AddEditSongViewModel.AddSection(string? sectionTypeName) parses via Enum.TryParse<SectionType>.
      Do NOT use integer or enum values directly in XAML CommandParameter — they won't bind correctly.

  - id: G14
    title: Testament enum values are Old/New — NOT OldTestament/NewTestament
    rule: >
      OpenAdoration.Domain.Enums.Testament has exactly two values: Old and New.
      Writing Testament.OldTestament or Testament.NewTestament → CS0117 compile error.
      This applies everywhere: OsisBookCatalog, TestamentToLabelConverter, ThiagobodrukJsonParser,
      and any future code that references the Testament enum.

  - id: G15
    title: CollectionViewSource SortDescription requires scm namespace
    rule: >
      To use <scm:SortDescription> in XAML you must declare:
        xmlns:scm="clr-namespace:System.ComponentModel;assembly=WindowsBase"
      Without this the SortDescription element is unresolved at compile time.
      Used in BibleView.xaml to sort books by BookNumber within each Testament group.

  - id: G16
    title: BibleViewModel.SelectedChapter uses 0 as "none" sentinel
    rule: >
      SelectedChapter is int (not int?). The chapters collection contains 1..N.
      Setting SelectedChapter=0 deselects the ListBox without a nullable binding hack,
      because 0 is never present in the collection.
      OnSelectedChapterChanged(0) correctly clears verses and skips the DB call
      (the guard is: if (value > 0 && ...) LoadVersesAsync(...)).

  - id: G17
    title: AlignToggleButton IsChecked must be OneWay
    rule: >
      The alignment strip uses ToggleButton with IsChecked="{Binding IsAlignX, Mode=OneWay}".
      Mode must be OneWay — TwoWay would let the button's checked state overwrite the VM property
      with a bool instead of firing the SetAlignmentCommand, causing a binding type mismatch.
      The Command+CommandParameter pattern drives state changes; IsChecked is read-only display.

# ─────────────────────────────────────────────────────────────────────────────
feature_status:  # as of 2026-05-18
  Songs:
    domain: done
    service: done
    repository: done
    viewmodel: done
    view: done
    working: true
    notes: >
      Full CRUD + search + projection. Sections managed as replace-all on update.
      Classification field added (migration AddSongClassification).
      Confirmed working 2026-05-12.

  Themes:
    domain: done
    service: done
    repository: done
    viewmodel: done
    view: done
    working: true
    notes: >
      Full theme editor with live preview. xctk:ColorPicker for font + background color.
      Font size ± controls (DecreaseFontSizeCommand / IncreaseFontSizeCommand, step 2, range 8–300).
      Text alignment strip (AlignToggleButton — Left / Center / Right).
      Background image and video support (MediaElement with LoadedBehavior=Manual for looping).
      ProjectionWindow reads Theme from IThemeService via IServiceScopeFactory and applies
      font family, size, color, and text alignment to SlideTextBlock on every slide change.
      B2 fixed 2026-05-18.

  Bible:
    domain: done
    service: done
    repository: done
    viewmodel: done
    view: done
    working: true
    notes: >
      3-column browser: Books (grouped by Testament with OT/NT headers) | Chapter tiles | Verses.
      Single-click on a verse immediately projects it (OnSelectedVerseChanged fires LoadSlides).
      Full-text search (SearchAsync, max 200 results); ClearSearch restores the chapter cache.
      Previous/Next verse navigation commands with CanExecute guards.
      Multi-format import: Zefania XML, OSIS XML (milestone + container), USFX XML,
      thiagobodruk JSON (array root or {books} wrapper), OpenAdoration JSON.
      Auto-detect format by extension + content sniffing in BibleFormatDispatcher.
      Confirmed working 2026-05-18.

  ServiceSchedule:
    domain: done
    service: partial
    repository: partial
    viewmodel: stub
    view: stub
    working: false
    notes: Basic service CRUD done. Schedule item management methods not yet added.

  Media:
    domain: done
    service: done
    repository: done
    viewmodel: stub
    view: stub
    working: false
    notes: ProjectionWindow already handles SlideType.Media. No import UI yet.

  Projection:
    working: true
    notes: >
      Engine fully implemented. ProjectionWindow renders text/media/blank.
      Theme now applied per slide via IServiceScopeFactory → IThemeService (B2 fixed).
      Font family, size, color, and text alignment are all driven by the Theme entity.

# ─────────────────────────────────────────────────────────────────────────────
confirmed_bugs:
  - id: B1
    status: FIXED  # 2026-05-11
    location: OpenAdoration.Application/Common/Slide.cs
    description: Slide.Blank() always threw ArgumentException — content check didn't exempt SlideType.Blank
    fix: "type is not SlideType.Media and not SlideType.Blank"

  - id: B2
    status: FIXED  # 2026-05-18
    location: OpenAdoration.WPF/ProjectionWindow.xaml.cs
    description: Font/size/colour was hardcoded (Arial 72pt white). Slide.ThemeId was never read.
    fix: >
      ProjectionWindow receives IServiceScopeFactory via constructor injection.
      On each SlideChanged event, creates a scope, resolves IThemeService, loads the theme
      (by ThemeId if set, else the default), and applies FontFamily, FontSize, FontColor,
      and TextAlignment to SlideTextBlock. ParseTextAlignment() converts "Left"/"Right"/"Center"
      string to System.Windows.TextAlignment enum.

  - id: B3
    status: RESOLVED_BY_FRAMEWORK
    location: OpenAdoration.WPF/ViewModels/SongsViewModel.cs
    description: DeleteSongAsync is async Task → CommunityToolkit wraps in AsyncRelayCommand which handles exceptions correctly
    notes: Verified — no unobserved exception path. Item closed.

  - id: B4
    status: FIXED  # 2026-05-11
    location: OpenAdoration.WPF/ViewModels/MainViewModel.cs
    description: Root IServiceProvider captured scoped services (ISongService etc.) for the app lifetime
    fix: NavigateTo<T>() creates IServiceScope, resolves VM from it, disposes old scope on next navigation

  - id: B5
    status: FIXED  # 2026-05-12
    location: OpenAdoration.Application/Services/SongService.cs
    description: GenerateSlides would throw ArgumentException if any section had empty Lyrics (SlideType.Song requires content)
    fix: Added .Where(s => !string.IsNullOrWhiteSpace(s.Lyrics)) filter before Slide construction; logs warning for skipped sections

  - id: B8
    status: FIXED  # 2026-05-11
    location: OpenAdoration.Infrastructure/Repositories/SongRepository.cs — UpdateAsync
    description: Classification field was silently dropped on every song edit (only Title and Author were copied to the tracked entity)
    fix: Added existing.Classification = song.Classification

  - id: B6
    status: OPEN
    location: OpenAdoration.WPF/MainWindow.xaml.cs — OnContentRendered
    description: ProjectionWindow opens automatically on every app launch, covering the primary screen if no secondary monitor is present
    planned_fix: >
      Remove ShowOnSecondaryScreen() call from OnContentRendered.
      Window stays hidden at startup. First ProjectSong call opens + projects
      in one step. Add "Open Screen" button in bottom bar (milestone_0b).

  - id: B7
    status: OPEN
    location: OpenAdoration.WPF/ProjectionWindow.xaml
    description: Song title and section label are not shown on the projection screen — the operator cannot see which slide is active at a glance
    planned_fix: >
      Add a TextBlock overlay in the top-left corner of ProjectionWindow
      showing contextLabel (song title) and slide.Label (e.g. "Verse 1").
      Font small enough to not distract the audience. (milestone_0b)

# ─────────────────────────────────────────────────────────────────────────────
roadmap:
  milestone_0:
    title: Make Songs actually work
    status: DONE  # 2026-05-12
    fixes_done: [B1, B4, B5]
    delivered:
      - Full CRUD (add/edit/delete) with all SectionTypes
      - Classification field on Song (migration AddSongClassification)
      - Search by title
      - Projection via ProjectSongCommand
      - SongsView two-panel layout, AddEditSongView with section cards

  milestone_0b:
    title: Projection UX hardening
    status: PLANNED  # discussed 2026-05-12
    key_work:
      - "B6: Projection window must NOT open at startup — remove ShowOnSecondaryScreen()
         call from MainWindow.OnContentRendered. Window stays hidden until user
         explicitly projects. First ProjectSong call opens + projects in one step.
         Add 'Open Screen' button in bottom bar for showing a blank screen first."
      - "B7: Song title label in projection window corner — overlay TextBlock anchored
         top-left showing contextLabel (song title) + slide.Label (section).
         Receives data from IProjectionService.SlideChanged via slide.Label and
         a new ContextLabelChanged event or storing _contextLabel on the window."
      - "Preview panel in bottom bar — small 16:9 dark rectangle that mirrors the
         current slide state (lyrics text or blank indicator). Subscribes to
         SlideChanged. Works on both 1-screen and 2-screen setups. Replaces the
         plain CurrentSlideLabel TextBlock in MainWindow.xaml."
      - "Single-screen mode — ScreenHelper.GetSecondaryScreen() returns null.
         Instead of fullscreen-maximizing on the primary (blocks operator),
         open ProjectionWindow as a resizable floating window (WindowStyle=SingleBorderWindow,
         ResizeMode=CanResize, initial size ~800x450) positioned bottom-right.
         On dual-screen: keep existing fullscreen-on-secondary behaviour."

  milestone_1:
    title: Themes — apply to projection
    status: DONE  # 2026-05-18
    fixes_done: [B2]
    delivered:
      - Full theme CRUD — ThemeViewModel + ThemesView + AddEditThemeView
      - xctk:ColorPicker for font and background color (Extended.Wpf.Toolkit 5.0.0)
      - Font size ± increment/decrement controls (step 2, range 8–300)
      - Text alignment picker (Left / Center / Right toggle strip)
      - Background image and looping video support (MediaElement)
      - Live preview rectangle in AddEditThemeView
      - ProjectionWindow applies theme per slide (font family, size, color, alignment)
      - Migrations: AddThemeVideoBackground, AddThemeTextAlignment

  milestone_2:
    title: Bible Browser
    status: DONE  # 2026-05-18
    delivered:
      - 3-column browser (Books | Chapters | Verses) with OT/NT grouped book list
      - Chapter tiles (WrapPanel, 38×38 squares) — generated client-side from ChapterCount
      - Single-click verse projection (OnSelectedVerseChanged fires LoadSlides immediately)
      - Previous/Next verse navigation commands
      - Full-text search with ClearSearch to restore chapter view
      - Multi-format Bible import — Zefania XML, OSIS XML, USFX XML, thiagobodruk JSON, OpenAdoration JSON
      - Auto-format detection in BibleFormatDispatcher
      - OsisBookCatalog for canonical book name/number/testament lookup
      - Import progress status display (ImportStatus, IsImporting)
      - Delete version command

  milestone_3:
    title: Service Schedule
    status: PLANNED
    key_work:
      - Add schedule item management methods to IWorshipServiceService
      - ServiceScheduleViewModel — service picker + schedule builder + live mode
      - ServiceScheduleView.xaml

  milestone_4:
    title: Media
    status: PLANNED
    key_work:
      - Copy-on-import to %LocalAppData%\OpenAdoration\Media\
      - MediaViewModel + MediaView.xaml (WrapPanel of image thumbnails)
      - Note: video out of scope for MVP

  milestone_5:
    title: Keyboard Shortcuts
    status: PLANNED
    key_work: KeyDown handler in MainWindow.xaml.cs (not XAML bindings)
    shortcuts:
      Space/→/PageDown: Next slide
      ←/PageUp/Backspace: Previous slide
      B: Blank
      Escape: Stop
      "1-9": GoTo(N)
      Ctrl+1: Songs, Ctrl+2: Bible, Ctrl+3: Schedule

  milestone_6:
    title: Polish & Release
    status: PLANNED

# ─────────────────────────────────────────────────────────────────────────────
common_operations:
  build: "dotnet build --configuration Debug"
  run: "dotnet run --project OpenAdoration.WPF"

  add_migration: >
    dotnet ef migrations add <Name>
      --project OpenAdoration.Infrastructure
      --startup-project OpenAdoration.WPF

  reset_db: >
    Remove-Item "$env:LOCALAPPDATA\OpenAdoration\openadoration.db" -Force
    # Next launch re-creates via MigrateAsync()

  view_logs: >
    Get-Content "$env:LOCALAPPDATA\OpenAdoration\logs\openadoration-$(Get-Date -Format yyyyMMdd).log" -Tail 100 -Wait

  add_new_feature_checklist:
    - "Domain: entity likely already exists"
    - "Application: interface + service implementation"
    - "Infrastructure: repository implementation + entity configuration"
    - "WPF: replace stub ViewModel (in ViewModels/) with full implementation"
    - "WPF: replace stub View (in Views/) with full XAML"
    - "App.xaml: DataTemplate already registered — verify it matches ViewModel class name"
    - "Migration: only needed if schema changes; run add_migration command above"

  log_format: "2026-05-11 14:23:01.123 [INF] OpenAdoration.WPF.ViewModels.SongsViewModel: message"
  ef_noise: "Suppressed to Warning level — only slow/failed queries appear"

# ─────────────────────────────────────────────────────────────────────────────
patterns:
  viewmodel_load_pattern: |
    // In code-behind:
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MyViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }

  isBusy_guard_pattern: |
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;   // guard — callers must NOT set IsBusy before calling this
        IsBusy = true;
        ClearError();
        try { /* ... */ }
        catch (Exception ex) { _logger.LogError(ex, "..."); SetError("..."); }
        finally { IsBusy = false; }
    }

  section_vm_events_pattern: |
    // SongsViewModel wires up events on each SongSectionViewModel:
    private void SubscribeSectionEvents(SongSectionViewModel vm)
    {
        vm.MoveUpRequested   += OnMoveUp;
        vm.MoveDownRequested += OnMoveDown;
        vm.DeleteRequested   += OnDelete;
    }
    // Always unsubscribe in OnDelete and OnEditCancelled to prevent leaks.

  relaycommand_commandparameter_string: |
    // XAML:
    <Button Command="{Binding AddSectionCommand}" CommandParameter="Verse" />
    // ViewModel:
    [RelayCommand]
    private void AddSection(string? sectionTypeName)
    {
        Enum.TryParse<SectionType>(sectionTypeName, out var type);
        ...
    }

  relatviesource_inner_template: |
    <!-- Inside ItemsControl DataTemplate, to reach parent VM: -->
    Command="{Binding DataContext.SomeCommand,
              RelativeSource={RelativeSource AncestorType=UserControl}}"
    CommandParameter="{Binding}"   <!-- binds the item (DataContext of DataTemplate) -->

  on_song_saved_no_isbusy: |
    // CORRECT: let LoadAsync manage its own IsBusy
    private async void OnSongSaved(object? sender, Song song)
    {
        // ...unsubscribe, set IsEditing=false...
        try { await _songService.CreateAsync(song); }   // or UpdateAsync
        catch { SetError("..."); return; }
        await LoadAsync();  // LoadAsync sets IsBusy itself — do NOT set it here
    }

  bible_chapter_sentinel: |
    // SelectedChapter is int, not int?. 0 = nothing selected.
    // Collection is populated with 1..ChapterCount — 0 is never present,
    // so the ListBox deselects cleanly when SelectedChapter = 0.
    partial void OnSelectedChapterChanged(int value)
    {
        DisplayedVerses.Clear();
        if (value > 0 && SelectedVersion is not null && SelectedBook is not null)
            _ = LoadVersesAsync(SelectedVersion.Id, SelectedBook.Name, value);
    }

  text_alignment_vm_pattern: |
    // Store as WPF enum in the VM; convert to/from string at DB boundary only.
    [ObservableProperty] private System.Windows.TextAlignment _textAlignment
        = System.Windows.TextAlignment.Center;

    public bool IsAlignLeft   => TextAlignment == System.Windows.TextAlignment.Left;
    public bool IsAlignCenter => TextAlignment == System.Windows.TextAlignment.Center;
    public bool IsAlignRight  => TextAlignment == System.Windows.TextAlignment.Right;

    partial void OnTextAlignmentChanged(System.Windows.TextAlignment _)
    {
        OnPropertyChanged(nameof(IsAlignLeft));
        OnPropertyChanged(nameof(IsAlignCenter));
        OnPropertyChanged(nameof(IsAlignRight));
    }

    [RelayCommand]
    private void SetAlignment(string alignment) =>
        TextAlignment = alignment switch
        {
            "Left"  => System.Windows.TextAlignment.Left,
            "Right" => System.Windows.TextAlignment.Right,
            _       => System.Windows.TextAlignment.Center
        };

    // XAML strip (Mode=OneWay is mandatory — see G17):
    // <ToggleButton IsChecked="{Binding IsAlignCenter, Mode=OneWay}"
    //               Command="{Binding SetAlignmentCommand}" CommandParameter="Center" />
