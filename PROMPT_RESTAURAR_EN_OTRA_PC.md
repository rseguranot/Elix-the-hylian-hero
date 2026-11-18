# Prompt autónomo para restaurar Elix en otra PC

Copia desde **“Estoy usando Windows…”** hasta el final y pégalo en una conversación nueva de Codex en la otra computadora. No requiere el contexto de la conversación original.

```text
Estoy usando Windows y quiero restaurar, instalar y activar **Elix — The Hylian Hero** desde su repositorio oficial:

https://github.com/rseguranot/Elix-the-hylian-hero.git

No tienes contexto previo sobre este proyecto. Trabaja de forma autónoma siguiendo estas instrucciones.

Objetivo:
- Obtener la versión más reciente de la rama `main`.
- Restaurar todo el código, sprites y funciones de Elix.
- Compilar la aplicación para Windows.
- Instalarla en `%LOCALAPPDATA%\ElixTheHylianHero`.
- Configurar el inicio automático con Windows.
- Ejecutar la auto-prueba incluida.
- Dejar una sola instancia de Elix activa al terminar.

Autorización específica:
- Puedes clonar o actualizar este repositorio.
- Puedes compilarlo con las dependencias ya declaradas.
- Puedes ejecutar `installer\Install.ps1`.
- Puedes cerrar únicamente procesos cuyo ejecutable sea exactamente `%LOCALAPPDATA%\ElixTheHylianHero\ElixTheHylianHero.exe` cuando sea necesario para actualizar o probar.
- Puedes configurar para Elix el valor de inicio automático que ya administra el instalador bajo `HKCU`.
- Puedes iniciar Elix al finalizar.

Restricciones:
- No borres ni sobrescribas cambios locales que no estén confirmados en Git.
- No uses `git reset --hard` ni descartes archivos modificados.
- No instales herramientas globales ni cambies políticas de PowerShell sin consultarme.
- No uses `ExecutionPolicy Bypass`. Ejecuta el script directamente; si la política del equipo lo bloquea, informa el bloqueo y detente.
- No detengas otros procesos y no modifiques firewall ni antivirus.
- No publiques cambios nuevos en GitHub; esta tarea es únicamente de restauración e instalación.

Procedimiento:
1. Verifica que estén disponibles Git y el SDK de .NET 10. Si falta alguno, indícame exactamente cuál falta y detente antes de instalarlo.
2. Usa como carpeta recomendada `%USERPROFILE%\Documents\Elix-the-hylian-hero`, salvo que el workspace actual ya contenga el repositorio.
3. Si la carpeta no existe, clona el repositorio. Si ya existe y es un repositorio Git:
   - inspecciona `git status`;
   - si está limpio, ejecuta `git fetch origin` y actualiza `main` únicamente con avance rápido (`git pull --ff-only origin main`);
   - si tiene cambios locales, no los alteres: informa la situación y espera mi decisión.
4. Lee `README.md` y cualquier `AGENTS.md` aplicable antes de ejecutar la instalación.
5. Confirma que la rama activa sea `main` y registra el hash de `HEAD` y `origin/main`; ambos deben coincidir antes de instalar.
6. Desde la raíz del repositorio, ejecuta directamente:

   `& .\installer\Install.ps1`

7. Después de instalar, comprueba que exista:

   `%LOCALAPPDATA%\ElixTheHylianHero\ElixTheHylianHero.exe`

8. Para la auto-prueba:
   - cierra únicamente la instancia cuyo `Path` coincida exactamente con ese ejecutable;
   - ejecuta el instalable con `--self-test` mediante `Start-Process -Wait -PassThru`;
   - verifica código de salida `0` y que `%TEMP%\ElixTheHylianHero-self-test.txt` comience con `PASS`;
   - inicia de nuevo el ejecutable normalmente, sin ventana oculta.
9. Verifica al finalizar:
   - exactamente una instancia activa y respondiendo;
   - versión instalada y hash del commit;
   - `HEAD` igual a `origin/main`;
   - árbol de trabajo limpio;
   - inicio automático apuntando al ejecutable instalado;
   - seis cuadros en `assets\animations\idle-breathe` y seis en `assets\animations\idle-smile` dentro de la instalación;
   - auto-prueba completa aprobada.
10. Informa en español:
   - qué revisión se instaló;
   - dónde quedó instalada;
   - resultados de las verificaciones;
   - cualquier pendiente o bloqueo real.

Notas importantes:
- La posición y el tamaño de Elix son configuraciones locales de cada PC. Si es una instalación nueva, usa los valores predeterminados apropiados para esa pantalla.
- Si ya existía una instalación, el instalador debe conservar `settings.json`.
- No declares éxito si la auto-prueba no pasó o si Elix no quedó activo.
```

## Qué restaura este prompt

- Todas las animaciones y controles de Elix.
- Las microacciones de inactividad: parpadeo, respiración y sonrisa.
- El gancho con clic derecho, caminar durante el arrastre, combate, ocarina y teletransporte.
- El monitor local de actividad de Codex.
- El instalador, el inicio automático y las pruebas de diagnóstico.

No sincroniza la posición exacta en pantalla entre equipos, porque las coordenadas y la disposición de monitores pueden ser diferentes. Una instalación existente conserva su configuración local.
