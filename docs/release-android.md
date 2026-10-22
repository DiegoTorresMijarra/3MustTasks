# Release de Android (APK firmado en GitHub Releases)

Workflow: `.github/workflows/release-android.yml`. Se lanza al **crear un tag `v*`**. No se lanza en push a ramas ni en PR (para eso está `android.yml`, que genera el APK debug).

## Qué hace
1. `scripts/version-from-tag.sh` valida el tag (`vMAJOR.MINOR.PATCH[-sufijo]`) y deriva:
   - `ApplicationDisplayVersion` (versionName) = el tag sin la `v`: `v1.2.3` → `1.2.3`, `v1.2.3-rc.1` → `1.2.3-rc.1`.
   - `ApplicationVersion` (versionCode) = `MAJOR*1.000.000 + MINOR*1.000 + PATCH`: `v1.2.3` → `1002003`. Es determinista y crece con la versión (Android exige que crezca para poder actualizar). Límites: `MINOR` y `PATCH` < 1000. El sufijo no cuenta: `v1.2.3-rc.1` y `v1.2.3` darían el mismo versionCode, así que **no se puede actualizar de una a otra**; sube el PATCH si necesitas reemplazar una prerelease instalada.
2. Comprueba que existen los 4 secretos y decodifica el keystore a un fichero temporal del runner (se borra al final).
3. `dotnet publish src/ThreeMustTasks.App.Maui -f net10.0-android -c Release -p:AndroidPackageFormat=apk` con las propiedades de firma `AndroidKeyStore`, `AndroidSigningKeyStore`, `AndroidSigningStorePass`, `AndroidSigningKeyAlias`, `AndroidSigningKeyPass`. Las contraseñas se pasan como `env:NOMBRE`, así que no salen en la línea de comandos.
4. Renombra el APK a `3MustTasks-<tag>.apk`, lo verifica con `apksigner verify` y crea la Release (con notas autogeneradas; marcada *pre-release* si el tag lleva sufijo) con el APK adjunto.

## Preparación (una vez, por el usuario)

### 1. Generar el keystore
Hazlo **en tu máquina** (nunca en el repo ni lo subas a git). Necesitas `keytool` (viene con el JDK):

```
keytool -genkeypair -v -keystore 3musttasks-release.keystore -alias 3musttasks -keyalg RSA -keysize 2048 -validity 10000
```

Te pedirá una contraseña del keystore, otra de la clave y tus datos (nombre, etc.). Apunta alias y contraseñas.

> **Guarda una copia del keystore y las contraseñas en un sitio seguro (gestor de contraseñas).** Android solo deja actualizar la app con APK firmados por la **misma** clave: si la pierdes, los usuarios tendrán que desinstalar y reinstalar (perdiendo los datos locales).

### 2. Convertirlo a base64
- Git Bash / Linux / macOS: `base64 -w0 3musttasks-release.keystore > keystore.b64` (en macOS: `base64 -i 3musttasks-release.keystore -o keystore.b64`)
- PowerShell: `[Convert]::ToBase64String([IO.File]::ReadAllBytes("3musttasks-release.keystore")) | Set-Content -NoNewline keystore.b64`

### 3. Crear los secretos
Repo en GitHub → Settings → Secrets and variables → Actions → *New repository secret*:

| Secreto | Valor |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Contenido de `keystore.b64` |
| `ANDROID_KEYSTORE_PASSWORD` | Contraseña del keystore |
| `ANDROID_KEY_ALIAS` | Alias (`3musttasks` en el ejemplo) |
| `ANDROID_KEY_PASSWORD` | Contraseña de la clave |

Después borra `keystore.b64` (el `.keystore` original queda solo en tu copia segura). Si el workflow no encuentra algún secreto, falla al principio con un mensaje claro.

## Lanzar una versión
Desde la rama que quieras publicar (normalmente `main`, ya integrada y con CI en verde):

```
git tag v1.0.0
git push origin v1.0.0
```

(Para una prerelease: `v1.0.0-rc.1`.) Sigue el job en la pestaña *Actions* → *Release Android*. Al terminar, el APK aparece en *Releases*. Si falla, corrige y vuelve a lanzar: borra la Release incompleta si existe y el tag (`git push origin :refs/tags/v1.0.0`, `git tag -d v1.0.0`) y créalo de nuevo, o crea un tag nuevo.

## Instalar el APK
1. En el móvil Android (8.0+), abre la página de la Release (`https://github.com/<usuario>/3MustTasks/releases`) y descarga `3MustTasks-<tag>.apk`.
2. Ábrelo. Android pedirá permitir *instalar apps desconocidas* para el navegador o gestor de archivos que lo abre: concédelo.
3. Instalar y abrir. Concede el permiso de notificaciones (Android 13+) y, si quieres recordatorios fiables, excluye la app del ahorro de batería (ver ajustes de la app).
4. Para actualizar, instala el APK de una versión mayor encima: se conservan los datos siempre que esté firmado con el mismo keystore.

## Qué está verificado y qué no
Verificado en local (sin CI, sin keystore real):
- El script de versión con tags de ejemplo (`v1.2.3`, prerelease, ceros a la izquierda, tags inválidos y fuera de rango).
- `dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk` con exactamente los parámetros de firma del workflow y un keystore de prueba temporal (ya borrado): genera `com.threemusttasks.app-Signed.apk`; `apksigner verify` lo da por válido con el certificado de prueba y `aapt2 dump badging` muestra `versionCode='1002003' versionName='1.2.3'`.

**No verificado** (necesita GitHub y un móvil, lo debe comprobar el usuario):
- La ejecución del workflow en `ubuntu-latest` (secretos reales, `base64 --decode`, `apksigner` en el runner, `gh release create`).
- Que el APK se publique en Releases y se **instale y abra** en un dispositivo real.
- Una actualización sobre una versión ya instalada.
