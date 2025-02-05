In RocketMod, when a plugin developer updates a DLL, they have to stop the server because the DLL gets locked after it is loaded so u cannot replace it, nor delete it. This makes testing and updating plugins a hassle, as every change requires a full restart.
With this module, this limitation is removed. It patches RocketMod so that plugins can be dynamically reloaded, similar to OpenMod. Instead of locking the DLL, this module ensures that after each reload, only the updated DLL inside the plugin folder is loaded. This means developers can modify and reload plugins on the fly without interrupting the server, making development easier  
`Commands: "/rm unload" , "/rm reload"`,
To use it just put RocketModFix folder inside the modules folder of your server, and that's it 
I hope this will help anyone ^^
