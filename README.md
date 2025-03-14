RocketMod Plugin Reloading Module

🚧 Work in Progress 🚧

Overview

In RocketMod, when a plugin developer updates a DLL, the server must be stopped because the DLL gets locked after loading. This prevents replacing or deleting the DLL, making testing and updating plugins a hassle. Every change requires a full server restart.

This module removes that limitation by patching RocketMod, allowing plugins to be dynamically reloaded, similar to OpenMod. After each reload, only the updated DLL inside the plugin folder is loaded. This means developers can modify and reload plugins on the fly without interrupting the server, significantly improving the development experience.

-Command:
/rm reload

-Installation:
Place the RocketModPluginReloader folder inside the modules folder of your server.
Done!

Credits

Some code from OpenMod has been used in this repository.

Special thanks to:

-Trojaner

-Rube200

-iamsilk 

-Diffoz

License

This project includes code from OpenMod, which is licensed under MIT License.

Modifications have been made to enable dynamic reloading for RocketMod.

(it will create tons of config files atm, will fix it in the future to have only 1 with the name of the og dll)
