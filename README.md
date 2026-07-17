# Elix — The Hylian Hero

Elix es un compañero animado transparente para Windows que observa los eventos locales de Codex y reacciona según la actividad actual. Su nombre oficial está integrado en el ejecutable y el paquete; no es editable desde la interfaz. No lee ni envía el contenido de las conversaciones: clasifica solamente tipos de evento y nombres de herramientas guardados por Codex en `.codex/sessions`.

Versión actual: **1.4.2**.

## Instalar en otra PC

Requisitos:

- Windows 10 u 11 de 64 bits.
- [Git](https://git-scm.com/download/win).
- [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

Desde PowerShell:

```powershell
git clone https://github.com/rseguranot-creator/Elix-the-hylian-hero.git
cd .\Elix-the-hylian-hero
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Install.ps1
```

El instalador compila la versión `Release`, la copia en `%LOCALAPPDATA%\ElixTheHylianHero`, configura el inicio con Windows y activa a Elix. Para instalarlo sin inicio automático:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\Install.ps1 -SkipAutostart
```

Ejecutar nuevamente el mismo comando actualiza una instalación existente y conserva `settings.json`, incluyendo posición y tamaño.

## Reacciones

- Pensando/planificando: luz del bosque, observación y arco.
- Ejecutando: combos de espada, giro y concentración.
- Buscando: arco, gancho y exploración visual.
- Creando: bomba/escudo, giro y espada.
- Viendo/analizando: revisión, luz del bosque y arco.
- Esperando, éxito o fallo: gestos específicos ya existentes y nuevas variantes.

Cada actividad reproduce una sola secuencia y conserva su pose final. No vuelve a animarse hasta que Codex cambia de actividad. Al elegir una nueva secuencia, el selector descarta tanto la última variante global como la última usada para ese estado.

## Controles

- Clic izquierdo + arrastre: mueve al héroe normalmente; caminar a izquierda o derecha se mantiene en bucle mientras sostienes el botón, cambia de dirección después de apenas 3 píxeles de reversa y se detiene al soltarlo.
- Ctrl + clic izquierdo sobre Elix: ejecuta al azar un combo de espada, disparo de arco, ataque giratorio o bomba/escudo, sin repetir la acción anterior.
- Ctrl + doble clic izquierdo sobre Elix: toca la ocarina y activa el modo de teletransporte.
- Durante el modo de teletransporte, Ctrl + doble clic izquierdo en cualquier punto de la pantalla: Navi lleva los destellos hasta el destino y Elix reaparece allí. Esc o clic derecho cancela.
- Clic derecho + arrastre: el héroe se queda fijo mientras el gancho sigue el cursor; al soltar después de una distancia larga, se desplaza hasta el punto enganchado mientras la cadena se retrae.
- Ctrl + rueda del ratón: ajusta el tamaño entre 50% y 100%; el tamaño original es el máximo.
- Clic derecho sin arrastrar: abre el menú para elegir tamaño pequeño, mediano o máximo, pausar el monitor, cambiar el inicio con Windows o cerrar.
- El tamaño elegido se guarda automáticamente y se restaura al volver a iniciar.

## Diagnóstico

```powershell
dotnet run --project .\HylianHeroCompanion.csproj -- --demo
dotnet run --project .\HylianHeroCompanion.csproj -- --self-test
```

La prueba también deja un informe en `%TEMP%\ElixTheHylianHero-self-test.txt`.

## Estructura

- `assets/animations`: cuadros transparentes de todas las animaciones.
- `installer/Install.ps1`: compilación, instalación, inicio automático y activación.
- `AnimationEngine.cs`: reproducción one-shot y bucle de caminar mientras se arrastra.
- `ActivityMonitor.cs`: clasificación local de la actividad de Codex.
- `MainWindow.xaml.cs`: controles, gancho, ocarina, combate y teletransporte.

## Aviso

Proyecto fan no oficial, sin afiliación ni respaldo de Nintendo. Los nombres y personajes relacionados con *The Legend of Zelda* pertenecen a sus respectivos titulares. No se incluye una licencia que conceda derechos sobre esos elementos visuales.
