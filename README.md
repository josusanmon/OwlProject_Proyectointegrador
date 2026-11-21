# [Ruteros: sin paradas]

Prototipo del Proyecto Integrador de Sistemas Interactivos (UPGCH, 5.° semestre, grupo A1).
Juego de conducción de una combi en Unity.

## Integrantes y roles

| Integrante | Rol |
|---|---|
| Josue Daniel Sanchez Montoya | Modelado 3D (entorno y objetos) |
| Miguel Alejandro Gordillo Gordillo | Dirección de proyecto, desarrollo web, diseño UI y documentación |
| Mauricio Gomez Archundia | Programación (Unity) |
| Juan Pedro Coutiño Trujillo | Modelado 3D y composición musical |
| Maximiliano Coronel Huerta | Ilustración artística y diseño de interfaz |

## Motor

- Unity 6000.6.8f1 (URP)
- Plataforma del build: Windows 64-bit

## Cómo ejecutar el build

1. Descarga el archivo `.zip` desde la sección **Releases** de este repositorio.
2. Descomprime la carpeta completa (no ejecutes el `.exe` desde dentro del .zip).
3. Abre `Ruteros sin paradas.exe` (en la build v0.1 se llama `My project.exe`).
4. Si Windows muestra el aviso azul de SmartScreen, da clic en *Más información > Ejecutar de todas formas* (el build no está firmado).

## Controles

| Acción | Tecla |
|---|---|
| Acelerar | W / ↑ |
| Frenar / reversa | S / ↓ |
| Girar izquierda / derecha | A, D / ← → |
| Freno de mano | Espacio |
| Encender / apagar faros | L |

## Estado actual

**Funciona:**
- Movimiento de la combi con aceleración, frenado e inercia; el giro depende de la velocidad.
- Cámara en tercera persona con FOV dinámico que no atraviesa paredes.
- Velocímetro en pantalla (km/h).
- Cielo HDRI de atardecer, niebla y post-proceso con tonemapping **AgX**.
- Materiales PBR: pintura con barniz (clear coat), vidrio, hule, asfalto y concreto con texturas.
- Efectos: faros, luces de freno, humo del escape y de llantas, reflejos dinámicos.
- Terreno bajo la ciudad y colisiones con calles, pasos de cebra, banquetas y terreno.

**Falta:**
- Mapa completo del recorrido.
- Colocar en la escena los autos ya modelados (Tsurus, Vochos, Rams) y darles colliders, igual que a la señalización.

## Estructura del proyecto

```
Assets/
├── Materials/     materiales (Cielo_Atardecer = skybox)
├── Modelos/       .fbx de la combi, autos, calles y señalización
├── Referencias/   imágenes de referencia (no van en la build)
├── Scenes/        Ruta.unity (escena principal)
├── Scripts/       CombiController, CamaraSigueCombi, VelocimetroHUD
├── Settings/      URP y perfil de post-proceso
└── Texturas/      HDRI y texturas
```

## Gráficos

### Tonemapping AgX
URP solo trae Neutral y ACES, así que AgX (el de Blender) está hecho como efecto propio:
- `Assets/Scripts/Render/AgXRendererFeature.cs` + `Assets/Shaders/AgX.shader` (ya agregado en `PC_Renderer`).
- Se controla desde el Volume global con el override **Owl Project > AgX Tonemapping** (exposición, look Base/Punchy/Golden, saturación, contraste).
- Para que funcione, el override *Tonemapping* de URP debe estar en **None** y el URP Asset en **Grading Mode: High Dynamic Range** (ya configurado).

### Post-proceso (Volume global, perfil `SampleSceneProfile`)
Bloom, Screen Space Lens Flare, Motion Blur, Depth of Field (fondo lejano), Split Toning, White Balance,
Color Adjustments, Chromatic Aberration, Film Grain y Vignette. SSAO en el renderer.

### Iluminación global (GI) dinámica
Menú **Owl Project > Iluminación > Activar GI dinámica**:
- En Unity 6.6 agrega **Surface Cache GI** (GI en tiempo real, sin hornear) y **Screen Space Reflections**, si su versión de Unity los trae. También desactiva Static Batching, porque Surface Cache GI lo requiere.
- Si no están disponibles, configura **Adaptive Probe Volumes** con oclusión de cielo. Después usen **Owl Project > Iluminación > Hornear APV** y suban los archivos `.bytes` que se generan junto a la escena (van por LFS).

### Materiales
- `Materials/Vehiculos`, `Materials/Entorno` y `Materials/Senalizacion` reemplazan los materiales que venían dentro de los `.fbx` (remapeo en el *Import Settings* de cada modelo, pestaña **Materials**).
- Las calles y banquetas tienen UVs estiradas, por eso usan el shader **Owl Project/Lit Mundo (Suelo)**, que proyecta las texturas en coordenadas de mundo (*Tiling* = repeticiones por metro).
- Las texturas de `Texturas/Generadas` son procedurales (asfalto, concreto, terreno y humo). Pueden reemplazarlas por texturas reales, por ejemplo de ambientCG (licencia CC0).

### Imagen de referencia del mapa
`Referencia_Mapa` está en la capa **Referencia** con la etiqueta *EditorOnly*: se ve en la vista Scene para construir el mapa, pero no en el juego ni en la build.

> Antes de clonar, instalen Git LFS (`git lfs install`). Los modelos, texturas e imágenes se guardan con LFS.
