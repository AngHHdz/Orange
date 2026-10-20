namespace Orange.Core;

/// <summary>
/// Tiny localization: the English text is the key, and each language maps it to a translation.
/// A missing translation falls back to English, so new strings never break the UI.
/// </summary>
public static class Loc
{
    public static readonly Dictionary<string, string> Languages = new()
    {
        ["en"] = "English",
        ["es"] = "Español",
        ["pt"] = "Português",
    };

    /// <summary>Translate into the current language.</summary>
    public static string T(string english) => In(Settings.Current.Language, english);

    /// <summary>Translate into a specific language (the Settings window previews before saving).</summary>
    public static string In(string language, string english) =>
        Tables.TryGetValue(language, out var table) && table.TryGetValue(english, out var s) ? s : english;

    /// <summary>Translate, then fill {0}, {1}…</summary>
    public static string F(string english, params object[] args) => string.Format(T(english), args);

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["es"] = new()
        {
            // Wheel
            ["Convert"] = "Convertir", ["Tools"] = "Herramientas", ["Actions"] = "Acciones",
            ["Cancel"] = "Cancelar", ["move over the wheel"] = "pasa sobre la rueda",
            ["nothing for this file yet"] = "nada para este archivo", ["Files only"] = "Solo archivos",
            ["drag files or folders"] = "arrastra archivos o carpetas", ["{0} files"] = "{0} archivos",

            // Tray & app
            ["Orange is running"] = "Orange está activo",
            ["Drag a file and press {0} (convert), {1} (tools) or {2} (actions)."] = "Arrastra un archivo y pulsa {0} (convertir), {1} (herramientas) o {2} (acciones).",
            ["Convert files…"] = "Convertir archivos…", ["Settings…"] = "Configuración…", ["Open output log"] = "Abrir registro",
            ["Start with Windows"] = "Iniciar con Windows", ["Quit Orange"] = "Salir de Orange",
            ["Orange — file converter"] = "Orange — conversor de archivos", ["Choose files for Orange"] = "Elige archivos para Orange",

            // Notifications
            ["Done"] = "Listo", ["{0} files done"] = "{0} archivos listos", ["{0}: {1} failed"] = "{0}: {1} con error",
            ["Copied"] = "Copiado", ["{0} item(s) — paste anywhere with Ctrl+V"] = "{0} elemento(s) — pégalos con Ctrl+V",
            ["Path copied"] = "Ruta copiada", ["{0} paths"] = "{0} rutas",

            // Verbs (notification titles)
            ["Convert to {0}"] = "Convertir a {0}", ["Extract {0}"] = "Extraer {0}", ["Images → PDF"] = "Imágenes → PDF",
            ["Grayscale"] = "Blanco y negro", ["Remove metadata"] = "Quitar metadatos", ["Downscale to 720p"] = "Reducir a 720p",
            ["Speed up"] = "Acelerar", ["Extract audio"] = "Extraer audio", ["Pack as {0}"] = "Comprimir como {0}",
            ["Repack as {0}"] = "Recomprimir como {0}", ["Slides → {0}"] = "Diapositivas → {0}", ["Merge PDF"] = "Unir PDF",
            ["PDF → {0}"] = "PDF → {0}", ["Compress PDF"] = "Comprimir PDF", ["Extract pages"] = "Extraer páginas",
            ["Page numbers"] = "Numerar páginas",

            // Slice labels
            ["Compress"] = "Comprimir", ["Resize"] = "Redimensionar", ["Rotate"] = "Girar", ["Flip"] = "Voltear",
            ["B & W"] = "B y N", ["Clean"] = "Limpiar", ["Text"] = "Texto", ["Merge"] = "Unir", ["Mute"] = "Silenciar",
            ["Speed"] = "Velocidad", ["Audio"] = "Audio", ["Normalize"] = "Normalizar", ["Mono"] = "Mono",
            ["Split"] = "Dividir", ["Split…"] = "Dividir…", ["Extract…"] = "Extraer…", ["Numbers"] = "Números",
            ["Watermark…"] = "Marca de agua…", ["Watermark"] = "Marca de agua", ["Protect…"] = "Proteger…", ["Protect"] = "Proteger",
            ["Unlock…"] = "Desbloquear…", ["Unlock"] = "Desbloquear", ["Extract"] = "Extraer",
            ["Copy"] = "Copiar", ["Path"] = "Ruta", ["Move"] = "Mover", ["Duplicate"] = "Duplicar", ["Float"] = "Flotar",
            ["Terminal"] = "Terminal", ["Reveal"] = "Mostrar",

            // Slice hints
            ["photo"] = "foto", ["lossless"] = "sin pérdida", ["web"] = "web", ["tiny"] = "muy ligero", ["classic"] = "clásico",
            ["print"] = "imprenta", ["raw"] = "sin comprimir", ["icon"] = "icono", ["document"] = "documento", ["one file"] = "un archivo",
            ["animated"] = "animado", ["video"] = "video", ["audio only"] = "solo audio", ["universal"] = "universal",
            ["uncompressed"] = "sin comprimir", ["open"] = "libre", ["voice"] = "voz", ["studio"] = "estudio",
            ["to share"] = "para compartir", ["rich text"] = "texto enriquecido", ["plain text"] = "texto plano",
            ["web page"] = "página web", ["e-book"] = "libro digital", ["to print"] = "para imprimir", ["comma"] = "comas",
            ["tab"] = "tabulaciones", ["web table"] = "tabla web", ["handout"] = "documento", ["slides"] = "diapositivas",
            ["image per slide"] = "imagen por diapositiva", ["to folder"] = "a carpeta", ["repack"] = "recomprimir",
            ["subtitles"] = "subtítulos", ["text only"] = "solo texto", ["compress"] = "comprimir", ["into one PDF"] = "en un PDF",
            ["page images"] = "imágenes de página", ["smaller file"] = "más liviano", ["90° right"] = "90° derecha",
            ["mirror"] = "espejo", ["grayscale"] = "escala de grises", ["remove metadata"] = "quitar metadatos",
            ["read (OCR)"] = "leer (OCR)", ["into PDF"] = "en PDF", ["smaller MP4"] = "MP4 más liviano", ["downscale"] = "reducir",
            ["remove audio"] = "quitar audio", ["extract MP3"] = "extraer MP3", ["loudness"] = "volumen", ["1 channel"] = "1 canal",
            ["every page"] = "cada página", ["smaller PDF"] = "PDF más liviano", ["by ranges"] = "por rangos",
            ["some pages"] = "algunas páginas", ["page 1 / N"] = "página 1 / N", ["diagonal text"] = "texto diagonal",
            ["password"] = "contraseña", ["remove password"] = "quitar contraseña", ["scan → text"] = "escaneo → texto",
            ["to clipboard"] = "al portapapeles", ["copy path"] = "copiar ruta", ["to folder…"] = "a carpeta…",
            ["pin on top"] = "fijar encima", ["open here"] = "abrir aquí", ["in Explorer"] = "en el Explorador",

            // Prompts
            ["OK"] = "Aceptar",
            ["Split PDF"] = "Dividir PDF", ["One new PDF per range, separated by commas.\nExample: 1-3, 4-6, 7-"] = "Un PDF nuevo por rango, separados por comas.\nEjemplo: 1-3, 4-6, 7-",
            ["Which pages go into the new PDF?\nExample: 1, 3, 5-7"] = "¿Qué páginas van en el nuevo PDF?\nEjemplo: 1, 3, 5-7",
            ["Text to stamp across every page:"] = "Texto para estampar en cada página:", ["CONFIDENTIAL"] = "CONFIDENCIAL",
            ["Protect PDF"] = "Proteger PDF", ["Password needed to open the new PDF:"] = "Contraseña para abrir el nuevo PDF:",
            ["Unlock PDF"] = "Desbloquear PDF", ["Current password of the PDF:"] = "Contraseña actual del PDF:",
            ["Move to…"] = "Mover a…", ["Copy to…"] = "Copiar a…",

            // Errors
            ["This PDF is already as small as Orange can make it."] = "Este PDF ya está tan liviano como Orange puede dejarlo.",
            ["No text was found."] = "No se encontró texto.",
            ["\"{0}\" is outside this PDF (it has {1} pages)."] = "\"{0}\" está fuera de este PDF (tiene {1} páginas).",
            ["I don't understand \"{0}\". Use something like 1-3, 5, 8-"] = "No entiendo \"{0}\". Usa algo como 1-3, 5, 8-",

            // Settings window
            ["Orange Settings"] = "Configuración de Orange", ["General"] = "General", ["Language"] = "Idioma",
            ["Show notifications when a job finishes"] = "Mostrar notificaciones al terminar",
            ["Wheel"] = "Rueda", ["Size"] = "Tamaño", ["Key for Convert"] = "Tecla para Convertir",
            ["Key for Tools"] = "Tecla para Herramientas", ["Key for Actions"] = "Tecla para Acciones",
            ["Each mode needs a different key."] = "Cada modo necesita una tecla distinta.",
            ["Output"] = "Archivos nuevos", ["Save next to the original file"] = "Guardar junto al archivo original",
            ["Save everything in this folder:"] = "Guardar todo en esta carpeta:", ["Browse…"] = "Examinar…",
            ["Image quality (JPG, WebP, AVIF)"] = "Calidad de imagen (JPG, WebP, AVIF)",
            ["Engines"] = "Motores", ["found"] = "encontrado", ["not found"] = "no encontrado",
            ["Save"] = "Guardar", ["Choose a folder for the output files."] = "Elige una carpeta para los archivos nuevos.",
            ["Phone (Wi-Fi sharing)"] = "Teléfono (compartir por Wi-Fi)", ["Share files with my phone over Wi-Fi"] = "Compartir archivos con mi teléfono por Wi-Fi",
            ["Files from the phone go to:"] = "Los archivos del teléfono se guardan en:",

            // Phone sharing
            ["iPhone"] = "iPhone", ["send over Wi-Fi"] = "enviar por Wi-Fi", ["Connect phone…"] = "Conectar teléfono…",
            ["Received from your phone"] = "Recibido del teléfono", ["Ready on your phone"] = "Listo en tu teléfono",
            ["Folders can't be sent. Zip them first."] = "Las carpetas no se pueden enviar. Comprímelas primero.",
            ["{0} file(s) — open Orange on your phone"] = "{0} archivo(s) — abre Orange en tu teléfono",
            ["Connect your phone"] = "Conecta tu teléfono", ["Your phone must be on the same Wi-Fi as this PC."] = "Tu teléfono debe estar en el mismo Wi-Fi que esta PC.",
            ["Phone sharing is turned off in Settings."] = "Compartir con el teléfono está desactivado en Configuración.",
            ["This PC is not connected to a network."] = "Esta PC no está conectada a una red.", ["Phone sharing could not start."] = "No se pudo iniciar el uso compartido.",
            ["1. Open the iPhone Camera and point it at the code."] = "1. Abre la Cámara del iPhone y apunta al código.",
            ["2. Tap the link that appears. Orange opens in Safari."] = "2. Toca el enlace que aparece. Orange se abre en Safari.",
            ["3. Tap Share (□↑), then “Add to Home Screen”. Now Orange is an icon on your phone."] = "3. Toca Compartir (□↑) y luego “Agregar a inicio”. Orange queda como un ícono en tu teléfono.",
            ["Send from the PC: drag a file, press {0} and drop it on “iPhone”."] = "Enviar desde la PC: arrastra un archivo, pulsa {0} y suéltalo en “iPhone”.",
            ["Files from the phone arrive in: {0}"] = "Los archivos del teléfono llegan a: {0}",
            ["Optional: “Send to Orange” in the iPhone Share menu"] = "Opcional: “Enviar a Orange” en el menú Compartir del iPhone",
            ["In the Shortcuts app tap +, name it “Send to Orange”."] = "En la app Atajos toca + y llámalo “Enviar a Orange”.",
            ["Tap ⓘ and turn on “Show in Share Sheet”."] = "Toca ⓘ y activa “Mostrar en hoja para compartir”.",
            ["Add the action “Get Contents of URL”, with this URL:"] = "Agrega la acción “Obtener contenido de URL” con esta URL:",
            ["Method: POST · Request Body: Form · add a File field named “file” = Shortcut Input."] = "Método: POST · Cuerpo: Formulario · agrega un campo Archivo llamado “file” = Entrada del atajo.",
            ["Done. In Photos or Files, tap Share → Send to Orange."] = "Listo. En Fotos o Archivos toca Compartir → Enviar a Orange.",
            ["Open received folder"] = "Abrir carpeta de recibidos", ["Phone can't connect? Allow in firewall"] = "¿No conecta? Permitir en el firewall",
            ["Done. Try again on the phone."] = "Listo. Intenta de nuevo en el teléfono.", ["The firewall was not changed."] = "El firewall no se cambió.",
            ["New key"] = "Nueva clave", ["Phones that are already connected will need to scan the new code. Continue?"] = "Los teléfonos ya conectados tendrán que escanear el nuevo código. ¿Continuar?",
            ["Open"] = "Abrir", ["Download"] = "Descargar", ["Remove"] = "Quitar", ["Sending"] = "Enviando", ["Sent"] = "Enviado", ["Failed"] = "Error",
            ["Nothing here yet. On the PC, drag a file, press {0} and drop it on “iPhone”."] = "Nada todavía. En la PC, arrastra un archivo, pulsa {0} y suéltalo en “iPhone”.",
            ["Wi-Fi sharing with {0}"] = "Compartir por Wi-Fi con {0}", ["Send to PC"] = "Enviar a la PC", ["Photos, videos or any file"] = "Fotos, videos o cualquier archivo",
            ["From your PC"] = "Desde tu PC", ["Tip: tap Share, then “Add to Home Screen” to keep Orange one tap away."] = "Consejo: toca Compartir y luego “Agregar a inicio” para tener Orange a un toque.",
        },

        ["pt"] = new()
        {
            // Wheel
            ["Convert"] = "Converter", ["Tools"] = "Ferramentas", ["Actions"] = "Ações",
            ["Cancel"] = "Cancelar", ["move over the wheel"] = "passe sobre a roda",
            ["nothing for this file yet"] = "nada para este arquivo", ["Files only"] = "Só arquivos",
            ["drag files or folders"] = "arraste arquivos ou pastas", ["{0} files"] = "{0} arquivos",

            // Tray & app
            ["Orange is running"] = "Orange está ativo",
            ["Drag a file and press {0} (convert), {1} (tools) or {2} (actions)."] = "Arraste um arquivo e pressione {0} (converter), {1} (ferramentas) ou {2} (ações).",
            ["Convert files…"] = "Converter arquivos…", ["Settings…"] = "Configurações…", ["Open output log"] = "Abrir registro",
            ["Start with Windows"] = "Iniciar com o Windows", ["Quit Orange"] = "Sair do Orange",
            ["Orange — file converter"] = "Orange — conversor de arquivos", ["Choose files for Orange"] = "Escolha arquivos para o Orange",

            // Notifications
            ["Done"] = "Pronto", ["{0} files done"] = "{0} arquivos prontos", ["{0}: {1} failed"] = "{0}: {1} com erro",
            ["Copied"] = "Copiado", ["{0} item(s) — paste anywhere with Ctrl+V"] = "{0} item(ns) — cole com Ctrl+V",
            ["Path copied"] = "Caminho copiado", ["{0} paths"] = "{0} caminhos",

            // Verbs
            ["Convert to {0}"] = "Converter para {0}", ["Extract {0}"] = "Extrair {0}", ["Images → PDF"] = "Imagens → PDF",
            ["Grayscale"] = "Preto e branco", ["Remove metadata"] = "Remover metadados", ["Downscale to 720p"] = "Reduzir para 720p",
            ["Speed up"] = "Acelerar", ["Extract audio"] = "Extrair áudio", ["Pack as {0}"] = "Compactar como {0}",
            ["Repack as {0}"] = "Recompactar como {0}", ["Slides → {0}"] = "Slides → {0}", ["Merge PDF"] = "Juntar PDF",
            ["PDF → {0}"] = "PDF → {0}", ["Compress PDF"] = "Comprimir PDF", ["Extract pages"] = "Extrair páginas",
            ["Page numbers"] = "Numerar páginas",

            // Slice labels
            ["Compress"] = "Comprimir", ["Resize"] = "Redimensionar", ["Rotate"] = "Girar", ["Flip"] = "Espelhar",
            ["B & W"] = "P e B", ["Clean"] = "Limpar", ["Text"] = "Texto", ["Merge"] = "Juntar", ["Mute"] = "Silenciar",
            ["Speed"] = "Velocidade", ["Audio"] = "Áudio", ["Normalize"] = "Normalizar", ["Mono"] = "Mono",
            ["Split"] = "Dividir", ["Split…"] = "Dividir…", ["Extract…"] = "Extrair…", ["Numbers"] = "Números",
            ["Watermark…"] = "Marca d'água…", ["Watermark"] = "Marca d'água", ["Protect…"] = "Proteger…", ["Protect"] = "Proteger",
            ["Unlock…"] = "Desbloquear…", ["Unlock"] = "Desbloquear", ["Extract"] = "Extrair",
            ["Copy"] = "Copiar", ["Path"] = "Caminho", ["Move"] = "Mover", ["Duplicate"] = "Duplicar", ["Float"] = "Flutuar",
            ["Terminal"] = "Terminal", ["Reveal"] = "Mostrar",

            // Slice hints
            ["photo"] = "foto", ["lossless"] = "sem perda", ["web"] = "web", ["tiny"] = "muito leve", ["classic"] = "clássico",
            ["print"] = "gráfica", ["raw"] = "sem compressão", ["icon"] = "ícone", ["document"] = "documento", ["one file"] = "um arquivo",
            ["animated"] = "animado", ["video"] = "vídeo", ["audio only"] = "só áudio", ["universal"] = "universal",
            ["uncompressed"] = "sem compressão", ["open"] = "livre", ["voice"] = "voz", ["studio"] = "estúdio",
            ["to share"] = "para compartilhar", ["rich text"] = "texto formatado", ["plain text"] = "texto simples",
            ["web page"] = "página web", ["e-book"] = "e-book", ["to print"] = "para imprimir", ["comma"] = "vírgulas",
            ["tab"] = "tabulações", ["web table"] = "tabela web", ["handout"] = "documento", ["slides"] = "slides",
            ["image per slide"] = "imagem por slide", ["to folder"] = "para pasta", ["repack"] = "recompactar",
            ["subtitles"] = "legendas", ["text only"] = "só texto", ["compress"] = "compactar", ["into one PDF"] = "em um PDF",
            ["page images"] = "imagens das páginas", ["smaller file"] = "mais leve", ["90° right"] = "90° direita",
            ["mirror"] = "espelho", ["grayscale"] = "tons de cinza", ["remove metadata"] = "remover metadados",
            ["read (OCR)"] = "ler (OCR)", ["into PDF"] = "em PDF", ["smaller MP4"] = "MP4 mais leve", ["downscale"] = "reduzir",
            ["remove audio"] = "remover áudio", ["extract MP3"] = "extrair MP3", ["loudness"] = "volume", ["1 channel"] = "1 canal",
            ["every page"] = "cada página", ["smaller PDF"] = "PDF mais leve", ["by ranges"] = "por intervalos",
            ["some pages"] = "algumas páginas", ["page 1 / N"] = "página 1 / N", ["diagonal text"] = "texto diagonal",
            ["password"] = "senha", ["remove password"] = "remover senha", ["scan → text"] = "digitalização → texto",
            ["to clipboard"] = "para a área de transferência", ["copy path"] = "copiar caminho", ["to folder…"] = "para pasta…",
            ["pin on top"] = "fixar no topo", ["open here"] = "abrir aqui", ["in Explorer"] = "no Explorador",

            // Prompts
            ["OK"] = "OK",
            ["Split PDF"] = "Dividir PDF", ["One new PDF per range, separated by commas.\nExample: 1-3, 4-6, 7-"] = "Um PDF novo por intervalo, separados por vírgulas.\nExemplo: 1-3, 4-6, 7-",
            ["Which pages go into the new PDF?\nExample: 1, 3, 5-7"] = "Quais páginas vão no novo PDF?\nExemplo: 1, 3, 5-7",
            ["Text to stamp across every page:"] = "Texto para carimbar em cada página:", ["CONFIDENTIAL"] = "CONFIDENCIAL",
            ["Protect PDF"] = "Proteger PDF", ["Password needed to open the new PDF:"] = "Senha para abrir o novo PDF:",
            ["Unlock PDF"] = "Desbloquear PDF", ["Current password of the PDF:"] = "Senha atual do PDF:",
            ["Move to…"] = "Mover para…", ["Copy to…"] = "Copiar para…",

            // Errors
            ["This PDF is already as small as Orange can make it."] = "Este PDF já está tão leve quanto o Orange consegue deixar.",
            ["No text was found."] = "Nenhum texto foi encontrado.",
            ["\"{0}\" is outside this PDF (it has {1} pages)."] = "\"{0}\" está fora deste PDF (ele tem {1} páginas).",
            ["I don't understand \"{0}\". Use something like 1-3, 5, 8-"] = "Não entendi \"{0}\". Use algo como 1-3, 5, 8-",

            // Settings window
            ["Orange Settings"] = "Configurações do Orange", ["General"] = "Geral", ["Language"] = "Idioma",
            ["Show notifications when a job finishes"] = "Mostrar notificações ao terminar",
            ["Wheel"] = "Roda", ["Size"] = "Tamanho", ["Key for Convert"] = "Tecla para Converter",
            ["Key for Tools"] = "Tecla para Ferramentas", ["Key for Actions"] = "Tecla para Ações",
            ["Each mode needs a different key."] = "Cada modo precisa de uma tecla diferente.",
            ["Output"] = "Arquivos novos", ["Save next to the original file"] = "Salvar ao lado do arquivo original",
            ["Save everything in this folder:"] = "Salvar tudo nesta pasta:", ["Browse…"] = "Procurar…",
            ["Image quality (JPG, WebP, AVIF)"] = "Qualidade de imagem (JPG, WebP, AVIF)",
            ["Engines"] = "Motores", ["found"] = "encontrado", ["not found"] = "não encontrado",
            ["Save"] = "Salvar", ["Choose a folder for the output files."] = "Escolha uma pasta para os arquivos novos.",
            ["Phone (Wi-Fi sharing)"] = "Celular (compartilhar por Wi-Fi)", ["Share files with my phone over Wi-Fi"] = "Compartilhar arquivos com meu celular por Wi-Fi",
            ["Files from the phone go to:"] = "Os arquivos do celular vão para:",

            // Phone sharing
            ["iPhone"] = "iPhone", ["send over Wi-Fi"] = "enviar por Wi-Fi", ["Connect phone…"] = "Conectar celular…",
            ["Received from your phone"] = "Recebido do celular", ["Ready on your phone"] = "Pronto no seu celular",
            ["Folders can't be sent. Zip them first."] = "Pastas não podem ser enviadas. Compacte primeiro.",
            ["{0} file(s) — open Orange on your phone"] = "{0} arquivo(s) — abra o Orange no celular",
            ["Connect your phone"] = "Conecte seu celular", ["Your phone must be on the same Wi-Fi as this PC."] = "Seu celular precisa estar no mesmo Wi-Fi deste PC.",
            ["Phone sharing is turned off in Settings."] = "O compartilhamento com o celular está desativado nas Configurações.",
            ["This PC is not connected to a network."] = "Este PC não está conectado a uma rede.", ["Phone sharing could not start."] = "Não foi possível iniciar o compartilhamento.",
            ["1. Open the iPhone Camera and point it at the code."] = "1. Abra a Câmera do iPhone e aponte para o código.",
            ["2. Tap the link that appears. Orange opens in Safari."] = "2. Toque no link que aparece. O Orange abre no Safari.",
            ["3. Tap Share (□↑), then “Add to Home Screen”. Now Orange is an icon on your phone."] = "3. Toque em Compartilhar (□↑) e depois em “Adicionar à Tela de Início”. Agora o Orange é um ícone no celular.",
            ["Send from the PC: drag a file, press {0} and drop it on “iPhone”."] = "Enviar do PC: arraste um arquivo, pressione {0} e solte em “iPhone”.",
            ["Files from the phone arrive in: {0}"] = "Os arquivos do celular chegam em: {0}",
            ["Optional: “Send to Orange” in the iPhone Share menu"] = "Opcional: “Enviar ao Orange” no menu Compartilhar do iPhone",
            ["In the Shortcuts app tap +, name it “Send to Orange”."] = "No app Atalhos toque em + e dê o nome “Enviar ao Orange”.",
            ["Tap ⓘ and turn on “Show in Share Sheet”."] = "Toque em ⓘ e ative “Mostrar na Planilha de Compartilhamento”.",
            ["Add the action “Get Contents of URL”, with this URL:"] = "Adicione a ação “Obter Conteúdo da URL” com esta URL:",
            ["Method: POST · Request Body: Form · add a File field named “file” = Shortcut Input."] = "Método: POST · Corpo: Formulário · adicione um campo Arquivo chamado “file” = Entrada do Atalho.",
            ["Done. In Photos or Files, tap Share → Send to Orange."] = "Pronto. Em Fotos ou Arquivos toque em Compartilhar → Enviar ao Orange.",
            ["Open received folder"] = "Abrir pasta de recebidos", ["Phone can't connect? Allow in firewall"] = "Não conecta? Permitir no firewall",
            ["Done. Try again on the phone."] = "Pronto. Tente de novo no celular.", ["The firewall was not changed."] = "O firewall não foi alterado.",
            ["New key"] = "Nova chave", ["Phones that are already connected will need to scan the new code. Continue?"] = "Celulares já conectados vão precisar escanear o novo código. Continuar?",
            ["Open"] = "Abrir", ["Download"] = "Baixar", ["Remove"] = "Remover", ["Sending"] = "Enviando", ["Sent"] = "Enviado", ["Failed"] = "Erro",
            ["Nothing here yet. On the PC, drag a file, press {0} and drop it on “iPhone”."] = "Nada ainda. No PC, arraste um arquivo, pressione {0} e solte em “iPhone”.",
            ["Wi-Fi sharing with {0}"] = "Compartilhando por Wi-Fi com {0}", ["Send to PC"] = "Enviar ao PC", ["Photos, videos or any file"] = "Fotos, vídeos ou qualquer arquivo",
            ["From your PC"] = "Do seu PC", ["Tip: tap Share, then “Add to Home Screen” to keep Orange one tap away."] = "Dica: toque em Compartilhar e depois em “Adicionar à Tela de Início” para ter o Orange a um toque.",
        },
    };
}
