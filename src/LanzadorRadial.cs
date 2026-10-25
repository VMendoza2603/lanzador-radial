// Lanzador radial: boton flotante abajo a la derecha que despliega tus aplicaciones en un arco.
// Clic izquierdo: abre o cierra el arco (4 apps). Rueda del raton: siguientes 4.
// Clic derecho: abre o cierra las opciones (+ agregar, carpeta de apps, icono en el escritorio, x salir).
// Las apps son los accesos directos de la carpeta "apps" junto al .exe (se ordenan por nombre: 01 ..., 02 ...).
// Nunca le quita el foco a la ventana en la que estas trabajando.
// Compilar: compilar.cmd (usa el compilador de C# que trae Windows).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Lanzador radial")]
[assembly: AssemblyProduct("Lanzador radial")]
[assembly: AssemblyDescription("Boton flotante que abre tus aplicaciones en un arco")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

public class AppItem { public string Path; public string Name; public Bitmap Icon; }

public static class Win {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint attr;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string type; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct PUNTO { public int X, Y; public PUNTO(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct TAMANO { public int W, H; public TAMANO(int w, int h) { W = w; H = h; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLEND { public byte Op, Flags, Alpha, Format; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint n);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] static extern int StrCmpLogicalW(string a, string b);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")]
    static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref PUNTO pos, ref TAMANO size, IntPtr src, ref PUNTO srcPos, int key, ref BLEND blend, int flags);

    public static void DpiAware() { try { SetProcessDPIAware(); } catch (Exception) { } }
    public static int Natural(string a, string b) { return StrCmpLogicalW(a, b); }
    public static void PermitirPrimerPlano() { try { AllowSetForegroundWindow(-1); } catch (Exception) { } }

    // Encima de todo sin activar la ventana (SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE).
    public static void AlFrente(IntPtr h) { SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x13); }

    // Pinta un bitmap con transparencia por pixel en una ventana en capas.
    public static void Capa(IntPtr h, Bitmap bmp, Point pos) {
        IntPtr pantalla = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(pantalla);
        IntPtr hb = bmp.GetHbitmap(Color.FromArgb(0)), viejo = SelectObject(mem, hb);
        try {
            var p = new PUNTO(pos.X, pos.Y); var s = new TAMANO(bmp.Width, bmp.Height); var o = new PUNTO(0, 0);
            var b = new BLEND { Op = 0, Flags = 0, Alpha = 255, Format = 1 };   // AC_SRC_ALPHA
            UpdateLayeredWindow(h, pantalla, ref p, ref s, mem, ref o, 0, ref b, 2);   // ULW_ALPHA
        } finally {
            SelectObject(mem, viejo); DeleteObject(hb); DeleteDC(mem); ReleaseDC(IntPtr.Zero, pantalla);
        }
    }

    // Icono grande (32 px a 100%) de un acceso directo, programa o carpeta, sin la flecha de acceso directo:
    // en un .lnk se usa el icono que el acceso indica o el de su destino.
    public static Bitmap IconoGrande(string path) {
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) {
            try {
                object sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                object acc = sh.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { path });
                string ubic = (string)acc.GetType().InvokeMember("IconLocation", BindingFlags.GetProperty, null, acc, null);
                string destino = Environment.ExpandEnvironmentVariables((string)acc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, acc, null));
                int coma = ubic.LastIndexOf(',');
                string archivo = Environment.ExpandEnvironmentVariables(coma >= 0 ? ubic.Substring(0, coma) : ubic).Trim();
                int indice = 0;
                if (coma >= 0) int.TryParse(ubic.Substring(coma + 1), out indice);
                if (archivo.Length > 0 && File.Exists(archivo)) {
                    var grande = new IntPtr[1];
                    if (ExtractIconEx(archivo, indice, grande, null, 1) > 0 && grande[0] != IntPtr.Zero) {
                        try { using (var ic = Icon.FromHandle(grande[0])) return ic.ToBitmap(); }
                        finally { DestroyIcon(grande[0]); }
                    }
                }
                if (destino.Length > 0 && (File.Exists(destino) || Directory.Exists(destino))) path = destino;
            } catch (Exception) { }
        }
        var info = new SHFILEINFO();
        SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), 0x100);   // SHGFI_ICON | SHGFI_LARGEICON
        if (info.hIcon == IntPtr.Zero) return null;
        try { using (var ic = Icon.FromHandle(info.hIcon)) return ic.ToBitmap(); }
        finally { DestroyIcon(info.hIcon); }
    }

    // La ventana activa ocupa toda la pantalla (juego, video): el boton se esconde para no estorbar.
    public static bool PantallaCompleta(Rectangle pantalla) {
        IntPtr h = GetForegroundWindow();
        if (h == IntPtr.Zero || h == GetDesktopWindow() || h == GetShellWindow()) return false;
        var sb = new StringBuilder(64);
        GetClassName(h, sb, 64);
        string c = sb.ToString();
        if (c == "WorkerW" || c == "Progman") return false;
        RECT r;
        if (!GetWindowRect(h, out r)) return false;
        return r.Left <= pantalla.Left && r.Top <= pantalla.Top && r.Right >= pantalla.Right && r.Bottom >= pantalla.Bottom;
    }
}

// Ventana transparente (en capas), sin bordes, siempre encima, fuera de la barra de tareas y que nunca toma el foco.
// Los pixeles totalmente transparentes dejan pasar el raton a la ventana de atras.
public class Flotante : Form {
    public Bitmap Ultimo;
    public Flotante() {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80000 | 0x80 | 0x8; return cp; }   // NOACTIVATE | LAYERED | TOOLWINDOW | TOPMOST
    }
    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; }   // WM_MOUSEACTIVATE -> MA_NOACTIVATE
        base.WndProc(ref m);
    }
    public void Pintar(Bitmap bmp) {
        if (Size != bmp.Size) Size = bmp.Size;
        Win.Capa(Handle, bmp, Location);
        if (Ultimo != null) Ultimo.Dispose();
        Ultimo = bmp;
    }
    // El punto (en pantalla) cae sobre algo pintado de esta ventana.
    public bool Toca(Point pantalla) {
        if (!Visible || Ultimo == null) return false;
        var p = PointToClient(pantalla);
        return p.X >= 0 && p.Y >= 0 && p.X < Ultimo.Width && p.Y < Ultimo.Height && Ultimo.GetPixel(p.X, p.Y).A > 0;
    }
}

public class Lanzador {
    const int PorPagina = 4, NumOpciones = 4, OpSalir = 3;
    const float Desde = 176f, Barrido = 98f;   // arco de izquierda (180) a arriba (270), con margen
    static readonly Color Cristal = Color.FromArgb(22, 25, 33), Acento = Color.FromArgb(59, 130, 246),
        Rojo = Color.FromArgb(239, 68, 68);

    static string AccesoEscritorio {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Lanzador radial.lnk"); }
    }
    static string NombreOpcion(int op) {
        switch (op) {
            case 0: return "Agregar aplicación";
            case 1: return "Abrir carpeta de aplicaciones";
            case 2: return File.Exists(AccesoEscritorio) ? "Quitar icono del escritorio" : "Crear icono en el escritorio";
            default: return "Salir del lanzador";
        }
    }

    readonly string carpeta;
    readonly float f;
    readonly int BTN, SOMBRA, LIENZO_B, ITEM, R_MID, OPC, R_OP, CR, MD, MARGEN;
    readonly List<AppItem> apps = new List<AppItem>();
    readonly float[] escalas = { 1f, 1f, 1f, 1f }, escalasOp = { 1f, 1f, 1f, 1f };
    int pagina, hover = -1, hoverOp = -1, ticks, direccion;
    float apertura, aperturaOp, transicion;
    bool abierto, abiertoOp, sobreBoton, ocultoPorPantallaCompleta;
    Rectangle pantalla;
    Point centro;
    readonly Font fuente;

    public readonly Flotante Boton = new Flotante();
    public readonly Flotante Rueda = new Flotante();
    readonly Timer vigia = new Timer(), anim = new Timer(), recargar = new Timer();
    readonly ToolTip tip = new ToolTip();
    FileSystemWatcher watcher;

    int S(int v) { return (int)Math.Round(v * f); }
    static float Suave(float t) { t = Math.Max(0f, Math.Min(1f, t)); return 1f - (1f - t) * (1f - t) * (1f - t); }   // ease-out

    public Lanzador(string carpeta) {
        this.carpeta = carpeta;
        Directory.CreateDirectory(carpeta);
        using (var g = Graphics.FromHwnd(IntPtr.Zero)) f = g.DpiX / 96f;
        BTN = S(50); SOMBRA = S(14); LIENZO_B = BTN + 2 * SOMBRA;
        ITEM = S(46); R_MID = S(140); OPC = S(30); R_OP = S(88); CR = S(236); MD = S(48); MARGEN = S(20);
        fuente = new Font("Segoe UI Semibold", S(13), FontStyle.Regular, GraphicsUnit.Pixel);

        // Boton: clic izquierdo abre/cierra las apps, clic derecho abre/cierra las opciones.
        Boton.Cursor = Cursors.Hand;
        Boton.AllowDrop = true;
        Boton.MouseEnter += delegate { sobreBoton = true; PintarBoton(); };
        Boton.MouseLeave += delegate { sobreBoton = false; PintarBoton(); };
        Boton.MouseUp += delegate(object s, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) { if (abierto) Cerrar(); else Abrir(); }
            else if (e.Button == MouseButtons.Right) { if (abiertoOp) CerrarOpciones(); else AbrirOpciones(); }
        };
        Boton.MouseWheel += delegate(object s, MouseEventArgs e) {
            if (!abierto) Abrir(); else CambiarPagina(e.Delta < 0 ? 1 : -1);
        };
        Boton.DragEnter += delegate(object s, DragEventArgs e) {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        };
        Boton.DragDrop += delegate(object s, DragEventArgs e) {
            var archivos = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (archivos != null) Agregar(archivos);
        };

        Rueda.Cursor = Cursors.Hand;
        Rueda.MouseMove += delegate(object s, MouseEventArgs e) { ActualizarHover(e.Location); };
        Rueda.MouseLeave += delegate { hover = -1; hoverOp = -1; anim.Start(); };
        Rueda.MouseWheel += delegate(object s, MouseEventArgs e) { CambiarPagina(e.Delta < 0 ? 1 : -1); };
        Rueda.MouseUp += delegate(object s, MouseEventArgs e) {
            if (e.Button == MouseButtons.Right) { if (abiertoOp) CerrarOpciones(); else AbrirOpciones(); return; }
            if (e.Button != MouseButtons.Left) return;
            int i = Indice(hover), op = hoverOp;
            if (i >= 0) { CerrarTodo(); Lanzar(apps[i]); }
            else if (op >= 0) Ejecutar(op);
        };

        tip.ShowAlways = true;

        anim.Interval = 15;
        anim.Tick += Animar;

        vigia.Interval = 100;
        vigia.Tick += Vigilar;

        recargar.Interval = 800;   // agrupa varios cambios seguidos en la carpeta
        recargar.Tick += delegate { recargar.Stop(); Cargar(); };
        watcher = new FileSystemWatcher(carpeta);
        watcher.SynchronizingObject = Boton;
        FileSystemEventHandler aviso = delegate { recargar.Stop(); recargar.Start(); };
        watcher.Created += aviso; watcher.Deleted += aviso; watcher.Changed += aviso;
        watcher.Renamed += delegate { recargar.Stop(); recargar.Start(); };
        watcher.EnableRaisingEvents = true;

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { Posicionar(); };
        Application.ThreadException += delegate { };   // un error en un clic no debe cerrar el programa
    }

    public void Iniciar() {
        Cargar();
        Posicionar();
        Boton.Show();
        PintarBoton();
        Win.AlFrente(Boton.Handle);
        vigia.Start();
    }

    void Posicionar() {
        var area = Screen.PrimaryScreen.WorkingArea;
        pantalla = Screen.PrimaryScreen.Bounds;
        centro = new Point(area.Right - MARGEN - BTN / 2, area.Bottom - MARGEN - BTN / 2);
        Boton.Location = new Point(centro.X - LIENZO_B / 2, centro.Y - LIENZO_B / 2);
        Rueda.Location = new Point(centro.X - CR, centro.Y - CR);
        if (Boton.Visible) PintarBoton();
    }

    void Cargar() {
        foreach (var a in apps) if (a.Icon != null) a.Icon.Dispose();
        apps.Clear();
        var rutas = new List<string>();
        try { rutas.AddRange(Directory.GetFiles(carpeta)); rutas.AddRange(Directory.GetDirectories(carpeta)); }
        catch (Exception) { }
        rutas.RemoveAll(delegate(string p) {
            try { return Path.GetFileName(p).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
                    || (File.GetAttributes(p) & FileAttributes.Hidden) != 0; }
            catch (Exception) { return true; }
        });
        rutas.Sort(delegate(string a, string b) { return Win.Natural(Path.GetFileName(a), Path.GetFileName(b)); });
        foreach (var p in rutas) {
            string baseName = Directory.Exists(p) ? Path.GetFileName(p) : Path.GetFileNameWithoutExtension(p);
            string nombre = Regex.Replace(baseName, @"^\d+[\s._-]*", "");
            apps.Add(new AppItem { Path = p, Name = nombre.Length > 0 ? nombre : baseName, Icon = Win.IconoGrande(p) });
        }
        if (pagina >= Paginas) pagina = 0;
        hover = -1;
        if (Rueda.Visible) PintarRueda();
    }

    int Paginas { get { return Math.Max(1, (apps.Count + PorPagina - 1) / PorPagina); } }
    int Indice(int posicion) { int i = pagina * PorPagina + posicion; return posicion >= 0 && i < apps.Count ? i : -1; }

    // ---------- abrir y cerrar ----------

    void MostrarRueda() {
        Rueda.Location = new Point(centro.X - CR, centro.Y - CR);
        if (!Rueda.Visible) { Rueda.Show(); PintarRueda(); }
        Win.AlFrente(Rueda.Handle);
        Win.AlFrente(Boton.Handle);   // el boton siempre queda por encima del arco
        anim.Start();
    }

    void Abrir() {
        if (apps.Count == 0) {
            tip.Show("Arrastra accesos directos a este botón\no usa clic derecho > +", Boton, -S(240), -S(40), 4000);
            return;
        }
        abierto = true; pagina = 0; hover = -1; transicion = 0;
        for (int k = 0; k < PorPagina; k++) escalas[k] = 1f;
        MostrarRueda();
    }

    void Cerrar() { if (abierto) { abierto = false; hover = -1; anim.Start(); } }

    void AbrirOpciones() {
        abiertoOp = true; hoverOp = -1;
        for (int k = 0; k < NumOpciones; k++) escalasOp[k] = 1f;
        MostrarRueda();
    }

    void CerrarOpciones() { if (abiertoOp) { abiertoOp = false; hoverOp = -1; anim.Start(); } }

    void CerrarTodo() { Cerrar(); CerrarOpciones(); }

    void CambiarPagina(int d) {
        int n = Paginas;
        if (n <= 1 || !abierto) return;
        pagina = (pagina + d + n) % n;
        direccion = d; transicion = 1f;
        hover = -1;
        ActualizarHover(Rueda.PointToClient(Cursor.Position));
        anim.Start();
    }

    PointF CentroItem(int posicion, float radio, float giro) {
        double a = (180.0 + 90.0 * (posicion + 0.5) / PorPagina + giro) * Math.PI / 180.0;
        return new PointF(CR + (float)(radio * Math.Cos(a)), CR + (float)(radio * Math.Sin(a)));
    }

    PointF CentroOpcion(int k, float radio) {
        double a = (180.0 + 90.0 * (k + 0.5) / NumOpciones) * Math.PI / 180.0;
        return new PointF(CR + (float)(radio * Math.Cos(a)), CR + (float)(radio * Math.Sin(a)));
    }

    static bool Dentro(Point p, PointF c, float r) { float dx = p.X - c.X, dy = p.Y - c.Y; return dx * dx + dy * dy <= r * r; }

    void ActualizarHover(Point p) {
        int nuevo = -1, nuevoOp = -1;
        if (abierto)
            for (int k = 0; k < PorPagina; k++) {
                if (Indice(k) < 0) break;
                if (Dentro(p, CentroItem(k, R_MID, 0), ITEM / 2 + S(5))) { nuevo = k; break; }
            }
        if (abiertoOp)
            for (int k = 0; k < NumOpciones; k++)
                if (Dentro(p, CentroOpcion(k, R_OP), OPC / 2 + S(4))) { nuevoOp = k; break; }
        if (nuevo == hover && nuevoOp == hoverOp) return;
        hover = nuevo; hoverOp = nuevoOp;
        anim.Start();
    }

    void Animar(object s, EventArgs e) {
        bool sigue = false;
        if (abierto && apertura < 1f) { apertura = Math.Min(1f, apertura + 0.075f); sigue = true; }
        if (!abierto && apertura > 0f) { apertura = Math.Max(0f, apertura - 0.13f); sigue = true; }
        if (abiertoOp && aperturaOp < 1f) { aperturaOp = Math.Min(1f, aperturaOp + 0.1f); sigue = true; }
        if (!abiertoOp && aperturaOp > 0f) { aperturaOp = Math.Max(0f, aperturaOp - 0.15f); sigue = true; }
        if (transicion > 0f) { transicion = Math.Max(0f, transicion - 0.1f); sigue = true; }
        sigue |= Acercar(escalas, hover, 1.14f);
        sigue |= Acercar(escalasOp, hoverOp, 1.15f);
        if (!abierto && !abiertoOp && apertura <= 0f && aperturaOp <= 0f) { Rueda.Hide(); sigue = false; }
        else PintarRueda();
        PintarBoton();
        if (!sigue) anim.Stop();
    }

    static bool Acercar(float[] escalas, int activo, float grande) {
        bool sigue = false;
        for (int k = 0; k < escalas.Length; k++) {
            float meta = k == activo ? grande : 1f;
            if (Math.Abs(escalas[k] - meta) > 0.004f) { escalas[k] += (meta - escalas[k]) * 0.35f; sigue = true; }
            else escalas[k] = meta;
        }
        return sigue;
    }

    void Ejecutar(int op) {
        CerrarTodo();
        if (op == 0) ElegirArchivos();
        else if (op == 1) Explorar(carpeta, null);
        else if (op == 2) AlternarAccesoEscritorio();
        else if (op == OpSalir) Salir();
    }

    // Crea (o quita) un icono en el escritorio que abre este .exe; un doble clic abre o cierra el lanzador.
    static void AlternarAccesoEscritorio() {
        try {
            string lnk = AccesoEscritorio;
            if (File.Exists(lnk)) { File.Delete(lnk); return; }
            string exe = Application.ExecutablePath;
            CrearAcceso(lnk, exe);
        } catch (Exception ex) {
            MessageBox.Show("No se pudo cambiar el icono del escritorio:\n" + ex.Message, "Lanzador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ---------- dibujo ----------

    static Graphics Lienzo(Bitmap b) {
        var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        return g;
    }

    static int A(float a, int v) { return Math.Max(0, Math.Min(255, (int)(v * a))); }

    // Sombra suave difuminada bajo un circulo.
    void Sombra(Graphics g, PointF c, float d, float a) {
        float m = S(7);
        using (var p = new GraphicsPath()) {
            p.AddEllipse(c.X - d / 2 - m, c.Y - d / 2 - m + S(3), d + 2 * m, d + 2 * m);
            using (var pb = new PathGradientBrush(p)) {
                pb.CenterColor = Color.FromArgb(A(a, 110), 0, 0, 0);
                pb.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                pb.FocusScales = new PointF(0.55f, 0.55f);
                g.FillPath(pb, p);
            }
        }
    }

    // Burbuja de cristal translucido: fondo oscuro semitransparente, brillo arriba y borde fino.
    void Burbuja(Graphics g, PointF c, float d, float a, bool activa, Color color) {
        Sombra(g, c, d, a);
        var rc = new RectangleF(c.X - d / 2, c.Y - d / 2, d, d);
        if (activa) {
            using (var p = new GraphicsPath()) {   // halo de color
                float m = S(9);
                p.AddEllipse(rc.X - m, rc.Y - m, rc.Width + 2 * m, rc.Height + 2 * m);
                using (var pb = new PathGradientBrush(p)) {
                    pb.CenterColor = Color.FromArgb(A(a, 120), color);
                    pb.SurroundColors = new[] { Color.FromArgb(0, color) };
                    pb.FocusScales = new PointF(0.7f, 0.7f);
                    g.FillPath(pb, p);
                }
            }
        }
        using (var b = new SolidBrush(Color.FromArgb(A(a, activa ? 215 : 150), activa ? color : Cristal))) g.FillEllipse(b, rc);
        using (var lg = new LinearGradientBrush(new RectangleF(rc.X, rc.Y - 1, rc.Width, rc.Height + 2),
                   Color.FromArgb(A(a, 60), 255, 255, 255), Color.FromArgb(A(a, 4), 255, 255, 255), 90f))
            g.FillEllipse(lg, rc);
        using (var pen = new Pen(Color.FromArgb(A(a, activa ? 170 : 75), 255, 255, 255), Math.Max(1f, f)))
            g.DrawEllipse(pen, rc.X + 0.5f, rc.Y + 0.5f, rc.Width - 1, rc.Height - 1);
    }

    // Simbolos de las opciones: + agregar, carpeta, pantalla (icono en el escritorio), x salir.
    void Simbolo(Graphics g, int op, PointF c, float escala, float a) {
        using (var pen = new Pen(Color.FromArgb(A(a, 240), 255, 255, 255), S(2) * escala)) {
            pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
            float l = S(7) * escala;
            if (op == 0) { g.DrawLine(pen, c.X - l, c.Y, c.X + l, c.Y); g.DrawLine(pen, c.X, c.Y - l, c.X, c.Y + l); }
            else if (op == OpSalir) { float x = l * 0.8f; g.DrawLine(pen, c.X - x, c.Y - x, c.X + x, c.Y + x); g.DrawLine(pen, c.X - x, c.Y + x, c.X + x, c.Y - x); }
            else if (op == 2) {
                float w = S(15) * escala, h = S(10) * escala, x0 = c.X - w / 2, y0 = c.Y - h / 2 - S(2) * escala;
                g.DrawRectangle(pen, x0, y0, w, h);
                g.DrawLine(pen, c.X, y0 + h, c.X, y0 + h + S(3) * escala);
                g.DrawLine(pen, c.X - S(4) * escala, y0 + h + S(4) * escala, c.X + S(4) * escala, y0 + h + S(4) * escala);
            }
            else {
                float w = S(15) * escala, h = S(11) * escala, x0 = c.X - w / 2, y0 = c.Y - h / 2 + S(1) * escala;
                using (var p = new GraphicsPath()) {
                    p.AddLine(x0, y0 + h, x0, y0 - S(2) * escala);
                    p.AddLine(x0, y0 - S(2) * escala, x0 + w * 0.4f, y0 - S(2) * escala);
                    p.AddLine(x0 + w * 0.4f, y0 - S(2) * escala, x0 + w * 0.5f, y0);
                    p.AddLine(x0 + w * 0.5f, y0, x0 + w, y0);
                    p.AddLine(x0 + w, y0, x0 + w, y0 + h);
                    p.CloseFigure();
                    g.DrawPath(pen, p);
                }
            }
        }
    }

    static GraphicsPath Pastilla(RectangleF r) {
        var p = new GraphicsPath(); float d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 90, 180); p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        p.CloseFigure(); return p;
    }

    // Etiqueta con el nombre: por encima (ancla abajo-centro) o a la izquierda (ancla derecha-medio).
    void Etiqueta(Graphics g, string texto, PointF ancla, bool izquierda, int lado) {
        var tam = g.MeasureString(texto, fuente);
        float w = tam.Width + S(20), h = tam.Height + S(8);
        float x = izquierda ? ancla.X - w : ancla.X - w / 2, y = izquierda ? ancla.Y - h / 2 : ancla.Y - h;
        x = Math.Max(S(4), Math.Min(lado - w - S(4), x));
        y = Math.Max(S(4), y);
        var r = new RectangleF(x, y, w, h);
        using (var p = Pastilla(r)) {
            using (var b = new SolidBrush(Color.FromArgb(225, 17, 19, 26))) g.FillPath(b, p);
            using (var pen = new Pen(Color.FromArgb(55, 255, 255, 255), Math.Max(1f, f))) g.DrawPath(pen, p);
        }
        using (var b = new SolidBrush(Color.White))
            g.DrawString(texto, fuente, b, r, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
    }

    void PintarRueda() {
        int lado = CR + MD;
        var bmp = new Bitmap(lado, lado, PixelFormat.Format32bppArgb);
        using (var g = Lienzo(bmp)) {
            // franjas invisibles (alfa 1): reciben la rueda del raton y los clics entre burbujas
            using (var b = new SolidBrush(Color.FromArgb(1, 0, 0, 0))) {
                if (abierto) FranjaInvisible(g, b, R_MID - ITEM / 2 - S(16), R_MID + ITEM / 2 + S(16));
                if (abiertoOp) FranjaInvisible(g, b, R_OP - OPC / 2 - S(8), R_OP + OPC / 2 + S(8));
            }

            for (int k = 0; k < PorPagina; k++) {
                int i = Indice(k);
                if (i < 0) break;
                float t = Suave((apertura - k * 0.08f) / 0.76f);
                float alfa = t * (1f - transicion);
                if (alfa <= 0.01f) continue;
                float radio = S(28) + (R_MID - S(28)) * t;
                var c = CentroItem(k, radio, direccion * transicion * 16f);
                float d = ITEM * (0.55f + 0.45f * t) * escalas[k];
                Burbuja(g, c, d, alfa, k == hover, Acento);
                var ic = apps[i].Icon;
                float lado_ic = (ic != null ? ic.Width : S(32)) * d / ITEM;
                var ri = new RectangleF(c.X - lado_ic / 2, c.Y - lado_ic / 2, lado_ic, lado_ic);
                if (ic != null) {
                    using (var ia = new ImageAttributes()) {
                        ia.SetColorMatrix(new ColorMatrix { Matrix33 = alfa });
                        g.DrawImage(ic, Rectangle.Round(ri), 0, 0, ic.Width, ic.Height, GraphicsUnit.Pixel, ia);
                    }
                } else using (var b = new SolidBrush(Color.FromArgb(A(alfa, 255), 255, 255, 255)))
                    g.DrawString(apps[i].Name.Substring(0, 1).ToUpper(), fuente, b, ri,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }

            // opciones pequenas en el arco interior
            for (int k = 0; k < NumOpciones; k++) {
                float t = Suave((aperturaOp - k * 0.1f) / 0.8f);
                if (t <= 0.01f) continue;
                var c = CentroOpcion(k, S(20) + (R_OP - S(20)) * t);
                float d = OPC * (0.55f + 0.45f * t) * escalasOp[k];
                Burbuja(g, c, d, t, k == hoverOp, k == OpSalir ? Rojo : Acento);
                Simbolo(g, k, c, d / OPC, t);
            }

            // puntos de pagina junto al boton
            int n = Paginas;
            if (n > 1 && apertura > 0f) {
                float rp = BTN / 2 + S(13), dp = S(5);
                double paso = 11.0, inicio = 225.0 - paso * (n - 1) / 2.0;
                for (int k = 0; k < n; k++) {
                    double a = (inicio + paso * k) * Math.PI / 180.0;
                    float x = CR + (float)(rp * Math.Cos(a)), y = CR + (float)(rp * Math.Sin(a));
                    float dd = k == pagina ? dp * 1.4f : dp;
                    using (var b = new SolidBrush(Color.FromArgb(A(apertura, k == pagina ? 235 : 110), 255, 255, 255)))
                        g.FillEllipse(b, x - dd / 2, y - dd / 2, dd, dd);
                }
            }

            // etiquetas encima de todo
            int ih = Indice(hover);
            if (ih >= 0 && apertura >= 1f && transicion <= 0f) {
                var c = CentroItem(hover, R_MID, 0);
                Etiqueta(g, apps[ih].Name, new PointF(c.X, c.Y - ITEM * 1.14f / 2 - S(8)), false, lado);
            }
            if (hoverOp >= 0 && aperturaOp >= 1f) {
                var c = CentroOpcion(hoverOp, R_OP);
                Etiqueta(g, NombreOpcion(hoverOp), new PointF(c.X - OPC * 1.15f / 2 - S(8), c.Y), true, lado);
            }
        }
        Rueda.Pintar(bmp);
    }

    void FranjaInvisible(Graphics g, Brush b, float rin, float rout) {
        using (var p = new GraphicsPath()) {
            p.AddArc(CR - rout, CR - rout, 2 * rout, 2 * rout, Desde, Barrido);
            p.AddArc(CR - rin, CR - rin, 2 * rin, 2 * rin, Desde + Barrido, -Barrido);
            p.CloseFigure();
            g.FillPath(b, p);
        }
    }

    void PintarBoton() {
        var bmp = new Bitmap(LIENZO_B, LIENZO_B, PixelFormat.Format32bppArgb);
        using (var g = Lienzo(bmp)) {
            var c = new PointF(LIENZO_B / 2f, LIENZO_B / 2f);
            float ap = Suave(apertura), tinte = Suave(Math.Max(apertura, aperturaOp));
            Sombra(g, c, BTN, 1f);
            var rc = new RectangleF(c.X - BTN / 2f, c.Y - BTN / 2f, BTN, BTN);
            // cristal oscuro que se tine de azul al abrir
            int r = (int)(Cristal.R + (Acento.R - Cristal.R) * tinte), gg = (int)(Cristal.G + (Acento.G - Cristal.G) * tinte),
                bl = (int)(Cristal.B + (Acento.B - Cristal.B) * tinte);
            using (var b = new SolidBrush(Color.FromArgb(sobreBoton ? 225 : 185, r, gg, bl))) g.FillEllipse(b, rc);
            using (var lg = new LinearGradientBrush(new RectangleF(rc.X, rc.Y - 1, rc.Width, rc.Height + 2),
                       Color.FromArgb(sobreBoton ? 80 : 60, 255, 255, 255), Color.FromArgb(4, 255, 255, 255), 90f))
                g.FillEllipse(lg, rc);
            using (var pen = new Pen(Color.FromArgb(sobreBoton ? 150 : 90, 255, 255, 255), Math.Max(1f, f)))
                g.DrawEllipse(pen, rc.X + 0.5f, rc.Y + 0.5f, rc.Width - 1, rc.Height - 1);

            // icono: cuatro puntos que giran y se convierten en una X al abrir las apps
            var estado = g.Save();
            g.TranslateTransform(c.X, c.Y);
            g.RotateTransform(45f * ap);
            float sep = S(6) * (1f - ap);
            using (var b = new SolidBrush(Color.FromArgb(A(1f - ap, 255), 255, 255, 255))) {
                float dp = S(7);
                for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        g.FillEllipse(b, i * (sep + S(1)) - dp / 2 + i * dp / 2 * (1 - ap), j * (sep + S(1)) - dp / 2 + j * dp / 2 * (1 - ap), dp, dp);
            }
            using (var pen = new Pen(Color.FromArgb(A(ap, 255), 255, 255, 255), S(3))) {
                pen.StartCap = pen.EndCap = LineCap.Round;
                float l = S(9);
                g.DrawLine(pen, -l, 0, l, 0);
                g.DrawLine(pen, 0, -l, 0, l);
            }
            g.Restore(estado);
        }
        Boton.Pintar(bmp);
    }

    // ---------- comportamiento ----------

    void Vigilar(object s, EventArgs e) {
        ticks++;
        // clic fuera del arco y del boton: cerrar todo
        if ((abierto || abiertoOp) && Control.MouseButtons != MouseButtons.None) {
            var p = Cursor.Position;
            if (!Boton.Toca(p) && !Rueda.Toca(p)) CerrarTodo();
        }
        if (ticks % 10 != 0) return;
        bool completa = Win.PantallaCompleta(pantalla);
        if (completa && Boton.Visible) {
            abierto = abiertoOp = false; apertura = aperturaOp = 0;
            Rueda.Hide(); Boton.Hide(); ocultoPorPantallaCompleta = true;
        }
        else if (!completa && ocultoPorPantallaCompleta) { Boton.Show(); PintarBoton(); Win.AlFrente(Boton.Handle); ocultoPorPantallaCompleta = false; }
        else if (!completa && Boton.Visible && ticks % 30 == 0) Win.AlFrente(Boton.Handle);
    }

    void Lanzar(AppItem a) {
        try {
            Win.PermitirPrimerPlano();
            Process.Start(new ProcessStartInfo(a.Path) { UseShellExecute = true });
        } catch (Exception ex) {
            MessageBox.Show("No se pudo abrir " + a.Name + ":\n" + ex.Message, "Lanzador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    static void Explorar(string carpeta, string seleccionar) {
        try {
            Win.PermitirPrimerPlano();
            if (seleccionar != null) Process.Start("explorer.exe", "/select,\"" + seleccionar + "\"");
            else Process.Start("explorer.exe", "\"" + carpeta + "\"");
        } catch (Exception) { }
    }

    // El dialogo necesita una ventana duena activa; si no, Windows lo abre escondido detras de todo.
    void ElegirArchivos() {
        using (var duena = new Form()) {
            duena.FormBorderStyle = FormBorderStyle.None; duena.ShowInTaskbar = false; duena.TopMost = true;
            duena.StartPosition = FormStartPosition.Manual; duena.Size = new Size(1, 1);
            duena.Location = new Point(centro.X - S(400), centro.Y - S(300)); duena.Opacity = 0;
            duena.Show(); duena.Activate();
            using (var d = new OpenFileDialog()) {
                d.Title = "Agregar aplicaciones al lanzador";
                d.Multiselect = true;
                d.DereferenceLinks = false;   // un acceso directo se copia tal cual
                d.Filter = "Programas y accesos directos|*.exe;*.lnk;*.url;*.bat;*.cmd|Todos los archivos|*.*";
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) + "\\Programs";
                if (d.ShowDialog(duena) == DialogResult.OK) Agregar(d.FileNames);
            }
        }
    }

    // Copia accesos directos o crea uno nuevo, numerado al final de la lista.
    void Agregar(string[] rutas) {
        int siguiente = 1;
        foreach (var a in apps) {
            var m = Regex.Match(Path.GetFileName(a.Path), @"^(\d+)");
            if (m.Success) siguiente = Math.Max(siguiente, int.Parse(m.Value) + 1);
        }
        siguiente = Math.Max(siguiente, apps.Count + 1);
        foreach (var r in rutas) {
            try {
                bool esCarpeta = Directory.Exists(r);
                string ext = esCarpeta ? "" : Path.GetExtension(r).ToLowerInvariant();
                string nombre = esCarpeta ? Path.GetFileName(r.TrimEnd('\\')) : Path.GetFileNameWithoutExtension(r);
                string prefijo = siguiente.ToString("D2") + " ";
                siguiente++;
                if (ext == ".lnk" || ext == ".url") File.Copy(r, Path.Combine(carpeta, prefijo + nombre + ext), false);
                else CrearAcceso(Path.Combine(carpeta, prefijo + nombre + ".lnk"), r);
            } catch (Exception ex) {
                MessageBox.Show("No se pudo agregar " + r + ":\n" + ex.Message, "Lanzador", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    static void CrearAcceso(string lnk, string destino) {
        object sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        object acc = sh.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
        var t = acc.GetType();
        t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, acc, new object[] { destino });
        string dir = Directory.Exists(destino) ? destino : Path.GetDirectoryName(destino);
        if (!string.IsNullOrEmpty(dir)) t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, acc, new object[] { dir });
        t.InvokeMember("Save", BindingFlags.InvokeMethod, null, acc, null);
    }

    public void Salir() {
        vigia.Stop(); anim.Stop();
        watcher.EnableRaisingEvents = false;
        Rueda.Hide(); Boton.Hide();
        Application.Exit();
    }
}

static class Programa {
    const string Unico = "Local\\LanzadorRadial", AvisoSalir = "Local\\LanzadorRadialSalir";

    // Abrir el .exe con el lanzador ya abierto lo cierra: asi un mismo icono sirve para abrir y cerrar.
    [STAThread]
    static void Main() {
        bool primero;
        using (var mutex = new Mutex(true, Unico, out primero)) {
            if (!primero) {
                EventWaitHandle aviso;
                if (EventWaitHandle.TryOpenExisting(AvisoSalir, out aviso)) { aviso.Set(); aviso.Dispose(); }
                return;
            }
            using (var salir = new EventWaitHandle(false, EventResetMode.AutoReset, AvisoSalir)) {
                Win.DpiAware();
                Application.EnableVisualStyles();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                var lanzador = new Lanzador(CarpetaApps());
                lanzador.Iniciar();
                ThreadPool.RegisterWaitForSingleObject(salir, delegate {
                    lanzador.Boton.BeginInvoke((MethodInvoker)lanzador.Salir);
                }, null, -1, true);
                Application.Run();
            }
        }
    }

    // Portable: la carpeta "apps" va junto al .exe. Si ahi no se puede escribir (por ejemplo en
    // Archivos de programa), se usa %APPDATA%\LanzadorRadial\apps.
    static string CarpetaApps() {
        string junto = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps");
        try {
            Directory.CreateDirectory(junto);
            string prueba = Path.Combine(junto, ".prueba");
            File.WriteAllText(prueba, "");
            File.Delete(prueba);
            return junto;
        } catch (Exception) {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LanzadorRadial", "apps");
        }
    }
}
