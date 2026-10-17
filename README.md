# FichApp

Aplicación web ASP.NET Core para fichar inicio y fin de jornada y las pausas, con cuentas individuales, histórico personal e informes CSV para administración.

## Requisitos

- SDK de .NET 10.
- Un directorio de datos persistente y protegido para SQLite.
- HTTPS y copias de seguridad comprobadas antes de usar la aplicación con datos laborales reales.

## Inicio local

En PowerShell, desde la carpeta del proyecto:

```powershell
dotnet user-secrets set "Fichaje:AdminEmail" "admin@tuempresa.es"
dotnet user-secrets set "Fichaje:AdminPassword" "Sustituye-por-una-clave-segura-12!"
dotnet run
```

Abre la dirección HTTPS que muestra `dotnet run` e inicia sesión con la cuenta administradora. Los secretos de usuario solo se usan en desarrollo; en producción, proporciona `Fichaje__AdminEmail` y `Fichaje__AdminPassword` mediante el gestor de secretos del entorno. La primera ejecución crea las tablas y los roles. La cuenta de administración solo se crea si aún no existe.

No hay registro público. Desde **Empleados**, la administración crea cuentas o restablece contraseñas, y entrega la contraseña inicial por un canal seguro; el empleado debe cambiarla al entrar. Las contraseñas requieren al menos 12 caracteres, mayúsculas, minúsculas, números y símbolos.

## Configuración

- `Fichaje:TimeZoneId`: zona horaria de la empresa; el valor inicial es `Europe/Madrid`. Configúrala según el centro de trabajo.
- `ConnectionStrings:DefaultConnection`: conexión SQLite. En producción, usa una ruta absoluta dentro de un volumen persistente y con acceso restringido, por ejemplo `Data Source=D:\datos\fichapp.db`.
- Para cambiar el esquema: `dotnet tool restore`, `dotnet ef database update` y despliega la nueva versión. La aplicación también aplica las migraciones pendientes al arrancar.

SQLite es para una instancia de la aplicación; no compartas el mismo fichero entre varias instancias. Sincroniza el reloj del servidor y limita el acceso al host y al volumen de datos. Para las copias, detén la aplicación antes de copiar la base de datos o utiliza una copia en caliente compatible con SQLite; copiar solo `app.db` mientras la aplicación escribe en el diario WAL puede omitir datos. Prueba las restauraciones y conserva copias de los registros durante al menos cuatro años.

## Registro y acceso

- Cada marca se genera con la hora del servidor, se guarda en UTC y se muestra en la zona horaria configurada.
- Se admiten inicio y fin de jornada y de pausas. No se recopilan ubicación ni biometría.
- Cada empleado consulta exclusivamente sus fichajes; administración puede consultar todas las marcas y descargarlas en CSV, en periodos de hasta 366 días.
- La aplicación no ofrece edición ni borrado de marcas. Conserva los originales; la organización debe establecer un procedimiento documentado para incidencias y rectificaciones, respetando sus acuerdos y las garantías de trazabilidad.
- No hay borrado automático: corresponde a la empresa aplicar el plazo de conservación pertinente y no eliminar los registros antes de cumplir las obligaciones legales.

## Alcance legal y protección de datos

El flujo cubre el núcleo técnico del artículo 34.9 del Estatuto de los Trabajadores: marca diaria de inicio y fin, conservación durante cuatro años y consulta por las personas trabajadoras y administración. La organización del sistema debe acordarse por negociación colectiva o acuerdo de empresa o, en su defecto, decidirse previa consulta a la representación legal de las personas trabajadoras.

La aplicación no determina convenios, calendarios, descansos, reglas de jornada, turnos ni particularidades de cada relación laboral. Antes de su uso, la empresa debe validar su configuración y procedimiento con asesoría laboral, dar la información de protección de datos que corresponda, identificar al responsable y base jurídica, y proteger adecuadamente el servidor, el almacenamiento y las copias.

Fuentes oficiales:

- [Estatuto de los Trabajadores, artículo 34.9 (BOE)](https://www.boe.es/buscar/act.php?id=BOE-A-2015-11430#a34).
- [Reglamento General de Protección de Datos (EUR-Lex)](https://eur-lex.europa.eu/eli/reg/2016/679/oj).

La página **Privacidad** es una plantilla y debe completarse con los datos y canales de contacto de la empresa antes de poner la aplicación en servicio.
