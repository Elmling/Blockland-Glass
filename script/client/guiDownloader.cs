//================================================
// Title: Glass Gui Client 
//================================================

if(!isObject(BLG_GDC)) new ScriptObject(BLG_GDC){ //Main Object
	SG = ""; //Clean-up group for disconnect
};
BLG_GDC.SG = new ScriptGroup();

//Protocol:
//----------
// - Register Objects
// - Register Attributes
// - Register Subclasses
// - Finalize parent objects

function BLG_GDC::verifyString(%this, %string) { //Checks sent message to make sure it has no unwanted code
	%illegal = ";";

	for(%i = 0; %i < strLen(%illegal); %i++) {
		if(strPos(%string, getSubStr(%illegal, %i, 1)) != -1) {
			return false;
		}
	}

	return true;
}

function BLG_GDC::finalizeObject(%this, %objId) {
	%obj = %this.SG.objData[%objId];
	if(isObject(%obj)) {
		%eval = "%obj = new " @ %obj.objClass @ "(" @ %obj.name @ "){";

		for(%i = 0; %i < %obj.attributes; %i++) {
			%eval = %eval @ %obj.attributeDat[%i] @ "=" @ %obj.attributeVal[%i] @ ";";
		}

		%eval = %eval @ "BLG__OBJID=" @ %objId @ ";";
		if(%obj.command !$= "") {
			%eval = %eval @ "command=" @ %obj.command @ ";";
		}
		%eval = %eval @ "};$BLG::GC::TempObj = %obj;";
		BLG.debug("Executing Object [" @ %objId @ "]...");
		BLG.debug(%eval, 3);
		eval(%eval);

		%obj.finalized = true;
		%obj.object = $BLG::GC::TempObj;

		for(%i = 0; %i < %obj.subclasses; %i++) {
			if(!%obj.subclass[%i].finalized) {
				%obj2 = %obj.subclass[%i].initiateObject();
			} else {
				%obj2 = %obj.subclass[%i].object;
			}
			%obj.add(%obj2);
		}

		%this.SG.add(%obj.object);
		return %obj.object;
	}
}

function BLG_GDC::setObjectAttribute(%this, %objId, %data, %value) {
	%obj = %this.SG.objData[%objId];
	%obj.attributeDat[%obj.attributes] = %data; 
	%obj.attributeVal[%obj.attributes] = %value;
	%obj.attributes++;
}

function BLG_GDC::setObjectChild(%this, %objId, %subId) {
	%obj = %this.SG.objData[%objId];
	%child = %this.SG.objData[%subId];
	if(%child.parent $= "") {
		%obj.child[%obj.children] = %child;
		%child.parent = %obj;
	}
}

function BLG_GDC::initiateObject(%this, %objId, %objClass, %name, %root) {
	%obj = new ScriptObject(BLG_GDC_ObjectConstructor) {
		id = %objId;
		finalized = false;

		root = %root; //bool - root object (gui, profile)
		name = %name; //string - object name
		objClass = %objClass; //string - object class (GuiWindow, GuiButton, GuiProfile)

		object = "";

		children = 0;
		attributes = 0;
	};

	%this.SG.objData[%objId] = %obj;
	%this.SG.add(%obj);
}

function BLG_GDC::handleMessage(%this, %msg) {
	%funcId = getField(%msg, 0);
	%objId = getField(%msg, 1);

	switch(%funcId) {
		case 0: //New Object
			if(BLG_GDCSG.objData[%objId] !$= "") {
				//Object already exists, someone broke the server mod
				BLG.debug("GUI Object sent for creation with objId [" @ %objId @ "] is using a already existing objId", 0);
			} else {
				%objClass = getField(%msg, 2);
				%name = getField(%msg, 3);
				%root = getField(%msg, 4);

				if(!%this.verifyString(%objClass) || !%this.verifyString(%name)) {
					//Watch out guys, we're dealing with a badass over here
					BLG.debug("Verify String test returned false for objId [" @ %objId @ "]. Could be possible attempt to run malicious script");
					BLG.debug("Message: [" @ %msg @ "]");
				}

				if(%objClass $= "") {
					BLG.debug("Invalid initiate message [" @ %msg @ "] (bad class)", 0);
				} else if(%root != 1 && %root != 0 && %root !$= "") {
					BLG.debug("Invalid initiate message [" @ %msg @ "] (bad root value)", 0);
				}

				if(%root) {
					%this.roots = trim(%this.roots SPC %objId);
				}
				%this.initiateObject(%this, %objId, %objClass, %name, %root);
			}

		case 1: //Set Attribute
			if(BLG_GDCSG.objData[%objId] $= "") {
				//Object already exists, someone broke the server mod
				BLG.debug("GUI Object attribute change for objId [" @ %objId @ "] is trying to change a non-existant object!", 0);
			} else {
				%data = getField(%msg, 2);
				%value = getField(%msg, 3);

				if(%data $= "") {
					BLG.debug("GUI Object attribute change for objId [" @ %objId @ "] is trying to change a blank attribute!", 0);
				}
				if(%value $= "command") {
					BLG.debug("GUI Object id [" @ %objId @ "] tried to register a callback invalidly. Possible attemp to run bad code.", 0);
				}

				%this.setObjectAttribute(%objId, %data, %value);
			}

		case 2: //Set Parent Object (After 0-1 are finished)
			%parentId = getField(%msg, 2);
			if(BLG_GDCSG.objData[%objId] $= "" || BLG_GDCSG.objData[%parentId] $= "") {
				BLG.debug("GUI Object id [" @ %objId @ "] and/or [" @ %parentId @ "] is invalid, but tried to parent", 0);
			} else {
				%this.setObjectChild(%objId, %parentId);
			}

		case 3: //Finished with setting items
			for(%i = 0; %i < getWordCount(%this.roots); %i++) {
				%this.finalizeObject(getWord(%this.roots, %i));
			}

		//All from here are non-generic calls

		case 4: //Set custom callback
			%command = "\"BLG_GDC.handleCallback(" @ %objId @ ");\"";
			%obj = BLG_GDCSG.objData[%objId];
			%obj.command = %command;
	}
}

function clientCmdBLG_ObjectInfo(%msg) {
	BLG_GDC.handleMessage(%msg);
}

function clientCmdMissionPreparePhaseBLG(%parts) {
	BLG_GDC.prepareParts = %parts;
	commandtoserver('MissionPreparePhaseBLGAck');
	echo(" *** Starting BLG GUI Download Phase - (" @ %parts @ " parts)");
}

package BLG_GDC_Package {
	function disconnectedCleanup() {
		parent::disconnectedCleanup();
		for(%i = 0; %i < BLG_GDCSG.getCount(); %i++) {
			%obj = BLG_GDCSG.getObject(%i);
			canvas.popDialog(%obj);
		}
		BLG_GDCSG.deleteAll();
	}
};

activatePackage(BLG_GDC_Package);