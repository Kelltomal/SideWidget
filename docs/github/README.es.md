# SideWidget

[Русский](../../README.md) · [English](README.en.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [Українська](README.uk.md) · [Беларуская](README.be.md) · **Español** · [Português (Brasil)](README.pt-BR.md)

SideWidget es un panel compacto con herramientas para el día a día en Windows. Muestra un reloj en el borde superior de la pantalla. Al hacer clic en él se abre el panel con la búsqueda de archivos, las aplicaciones ancladas y otras herramientas. El atajo predeterminado es `Ctrl+Alt+Space` (Ctrl+Alt+Espacio).

SideWidget usa el idioma de Windows y, si ese idioma no está disponible, se abre en inglés. Puedes cambiarlo en **Configuración → Idioma de la aplicación**.

SideWidget se desarrolla con sentido común y con la ayuda de GPT-6.1 Sol.

## Capturas de pantalla

<p align="center">
  <img src="images/sidewidget-home.png" width="760" alt="Panel principal de SideWidget con búsqueda, reproductor y accesos rápidos a las herramientas">
</p>

<p align="center">
  <img src="images/sidewidget-commands-es.png" width="590" alt="Ordenación de archivos en SideWidget en español">
</p>

La captura del reproductor se hizo en Windows mientras sonaba música. Las herramientas muestran datos de ejemplo.

## Ejemplos de comandos

Abre **Archivos y carpetas**, pega un comando y revisa el plan antes de aplicarlo.

- **Ordenar archivos.** `Ordena las facturas por año y deja las capturas de pantalla en el escritorio`
- **Iconos de carpetas.** `Cambia los iconos de las carpetas del escritorio`<br>`Cambia el icono de la carpeta a fotos`

[Los nueve idiomas](../../README.md#примеры-команд)

## Más herramientas

<details>
<summary><strong>Calculadora</strong></summary>

Calcula expresiones con porcentajes y paréntesis, copia el resultado o recupera un cálculo del historial.

<img src="images/sidewidget-tool-calculator.png" width="520" alt="Calculadora">

</details>

<details>
<summary><strong>Códigos QR</strong></summary>

Convierte un texto o enlace en un código QR y guárdalo en SVG, PNG o JPEG.

<img src="images/sidewidget-tool-qr.png" width="520" alt="Códigos QR">

</details>

<details>
<summary><strong>Conversor de unidades</strong></summary>

Convierte distancias, temperaturas y otras magnitudes. Guarda pares de unidades frecuentes.

<img src="images/sidewidget-tool-units.png" width="520" alt="Conversor de unidades">

</details>

<details>
<summary><strong>Cuentagotas</strong></summary>

Toma un color de la pantalla, copia su valor en HEX, RGB o HSL y guárdalo en tu paleta.

<img src="images/sidewidget-tool-colors.png" width="520" alt="Cuentagotas">

</details>

## Funciones

- Calculadora con historial, códigos QR en SVG, PNG y JPEG, conversión de unidades, cuentagotas de pantalla y conversión entre formatos de color.
- Búsqueda de archivos a través de Everything cuando está en ejecución y acceso rápido a las aplicaciones ancladas.
- **Archivos y carpetas.** Organiza los archivos del escritorio, previsualiza los iconos de las carpetas, elige un estilo común o uno por carpeta y deshaz el último cambio de iconos.
- Controles de reproducción para reproductores compatibles, mezclador de volumen y métricas del sistema.
- Calendario, borrador local en Markdown y traducción mediante servicios externos.
- Ajuste del tamaño y del atajo de teclado, y selección de las pantallas en las que aparece el panel.

## Instalación

Para Windows 10 u 11 x64. Abre [Releases](https://github.com/Kelltomal/SideWidget/releases/latest) y elige un ZIP.

- **Lite** (`-lite-win-x64.zip`) es la edición recomendada. Las descargas de actualizaciones son más pequeñas. Las dependencias se instalan una sola vez.
- **Completa** (`-portable-win-x64.zip`) incluye sus dependencias para funcionar sin conexión.

Extrae el ZIP en una carpeta propia. Para Lite, ejecuta allí [Install-Dependencies.bat](../../scripts/Install-Dependencies.bat). El script usa WinGet. Luego ejecuta `SideWidget.exe`, junto a la carpeta `App`.

### Instalar manualmente las dependencias de Lite

Instala estos paquetes x64 una sola vez. Los enlaces apuntan a instaladores de Microsoft.

- [.NET Runtime 10.0 x64](https://aka.ms/dotnet/10.0/dotnet-runtime-win-x64.exe)
- [ASP.NET Core Runtime 10.0 x64](https://aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe)
- [Windows App Runtime 1.8.12 x64](https://aka.ms/windowsappsdk/1.8/1.8.260921001/windowsappruntimeinstall-x64.exe)
- [Microsoft Edge WebView2 Evergreen](https://go.microsoft.com/fwlink/p/?LinkId=2124703)
- [Microsoft Visual C++ Redistributable v14 x64](https://aka.ms/vc14/vc_redist.x64.exe)

SideWidget busca versiones estables en GitHub al iniciarse. También puedes consultar **Configuración → Actualizaciones**. Lite se actualiza con su ZIP. Para pasar de la versión completa a Lite, cierra SideWidget desde la bandeja y ejecuta `SideWidget.exe` desde la carpeta de Lite. La versión completa también elige Lite para actualizarse cuando ya están instaladas todas las dependencias compartidas.

[Everything](../../README.md#everything) · [voidtools x64](https://www.voidtools.com/downloads/)

## Documentación del proyecto

[Compilar y contribuir](../../CONTRIBUTING.md) · [Referencia técnica en ruso](technical.ru.md) · [Informar de un fallo](https://github.com/Kelltomal/SideWidget/issues/new/choose) · [Informar de una vulnerabilidad](../../SECURITY.md) · [Traducir con Weblate](translating.md)
