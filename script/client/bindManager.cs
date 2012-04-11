//================================================
// Title: Glass Keybind Client 
//================================================

if(!isObject(BLG_GKC)) {
	new ScriptObject(BLG_GKC) {
		binds = 0;
	};
}

function BLG_GKC::load(%this) {
	%fo = new FileObject();
	%fo.openForRead("config/BLG/client/binds.txt");
	while(!%fo.isEOF()) {
		%line = %fo.readLine();
		if(BLG_GKC.bindName[getField(%line, 0)] $= "") {
			BLG_GKC.newBind(getField(%line, 0));
			BLG_GKC.setBind(getField(%line, 0), getField(%line, 1), getField(%line, 2));
		}
	}
	%fo.close();
	%fo.delete();
}

function BLG_GKC::save(%this) {
	%fo = new FileObject();
	%fo.openForWrite("config/BLG/client/binds.txt");
	for(%i = 0; %i < %this.binds; %i++) {
		%fo.writeLine(%this.bind[%i] TAB getField(%this.bindData[%i], 0) TAB getField(%this.bindData[%i], 1));
	}
	%fo.close();
	%fo.delete();
}

function BLG_GKC::setBind(%this, %name, %device, %key) {
	%this.bindData[%this.bindName[%name]] = %device TAB %key;
}

function BLG_GKC::newBind(%this, %name) {
	%this.bind[%this.binds] = %name;
	%this.bindName[%name] = %this.binds;
	eval("function BLG_BindCallback" @ %this.binds @ "(%t){BLG_GKC.bindCallback(" @ %this.binds @ ", %t);}");
	%this.binds++;
}

function BLG_GKC::activateBinds(%this) {
	for(%i = 0; %i < %this.binds; %i++) {
		GlobalActionMap.bind(getField(%this.bindData[%i], 0), getField(%this.bindData[%i], 1), "BLG_BindCallback" @ %i);
	}
}

function BLG_GKC::deactivateBinds(%this) {
	for(%i = 0; %i < %this.binds; %i++) {
		GlobalActionMap.unbind(getField(%this.bindData[%i], 0), getField(%this.bindData[%i], 1));
	}
}

function BLG_GKC::bindCallback(%this, %id, %tog) {
	if(%tog) {
		commandtoserver('BLG_BindCallback', %this.bindName[%id]);
	}
}

function clientcmdBLG_requireBind(%name) {
	if(BLG_GKC.bindName[%name] !$= "") {
		return;
	}
	BLG_GKC.newBind(%name);
}

package BLG_GKC_Package {
	function Canvas::pushDialog(%canvas, %gui) {
		%ret = parent::pushDialog(%canvas, %gui);

		if(BLG_GKC.playGui && %canvas.getCount() != 2) {
			BLG_GKC.deactivateBinds();
		}

		return %ret;
	}

	function Canvas::popDialog(%canvas, %gui) {
		%ret = parent::popDialog(%canvas, %gui);

		if(BLG_GKC.playGui) {
			if(%canvas.getCount() == 2) {
				BLG_GKC.activateBinds();
			}
		}

		return %ret;
	}

	function PlayGui::onWake(%gui) {
		parent::onWake(%gui);
		if(!BLG_GKC.playGui) {
			BLG_GKC.activateBinds();
		}

		BLG_GKC.playGui = true;

		for(%i = 0; %i < BLG_GKC.binds; %i++) {
			if(BLG_GKC.bindData[%i] $= "") {
				BLG_GKC.pushSetBindGui();
				break;
			}
		}
	}

	function PlayGui::onSleep(%gui) {
		parent::onSleep(%gui);
		BLG_GKC.playGui = false;
		BLG_GKC.deactivateBinds();
	}
};
activatePackage(BLG_GKC_Package);

BLG_GKC.newBind("BLG Test Bind");
BLG_GKC.setBind("BLG Test Bind", "keyboard", "b");