document.addEventListener('DOMContentLoaded', function() {
    writeSidebar();
    writeTOC();
});

function writeTOC(doc) {
    //Add a Table of Contents to the page body
    //Requires an element whose ID is "toc"

    var doc = doc || document;
    var tocElement = doc.getElementById('toc');

    if (tocElement === null)
        return;

    var tocTitleElement = doc.createElement('div');
    tocTitleElement.setAttribute('class', 'toctitle');
    tocTitleElement.textContent = "On This Page";
    tocElement.appendChild(tocTitleElement);

    var headers = [].slice.call(doc.body.querySelectorAll('h1, h2, h3, h4, h5, h6'));

    //Keep count of the headers for proper indexing
    var headerCount = [0, 0, 0, 0, 0, 0];

    headers.forEach(function(header, index) {
        // Create div
        var div = doc.createElement('div');
        var tag = header.tagName.toLowerCase();
        var headerHierarchy = tag.match(/\d+/)[0] - 1;

        var prefix = '';
        headerCount[headerHierarchy] += 1;
        //Set all counts to 0 that have a hierarchy value greater than this header
        //Otherwise append to the prefix
        for (let headerIndex = 0; headerIndex < headerCount.length; headerIndex++) {
            if (headerIndex < headerHierarchy) {
                prefix += headerCount[headerIndex] + '.';
            } else if (headerIndex == headerHierarchy) {
                prefix += headerCount[headerIndex] + ' ';
            } else if (headerIndex > headerHierarchy) {
                headerCount[headerIndex] = 0;
            }
        }


        div.setAttribute('class', 'toc' + header.tagName.toLowerCase());
        var urlSafeString = header.textContent.replace(/[.,\/#?!$%\^&\*;:{}='"\-_`~()]/g,"").replace(/[^a-z0-9_]+/gi, '_').replace(/^-|-$/g, '').toLowerCase();

        //Make a link element and add to div
        var linkElement = doc.createElement('a');
        linkElement.setAttribute('href', '#' + urlSafeString);
        linkElement.textContent = prefix + header.textContent;
        div.appendChild(linkElement);

        //Add div to table of contents
        tocElement.appendChild(div);

        //Create the anchor and add to original header in body
        var anchorElement = doc.createElement('a');
        anchorElement.setAttribute('name', 'header' + index);
        anchorElement.setAttribute('id', urlSafeString);
        header.parentNode.insertBefore(anchorElement, header);

    });
}

function writeSidebar(doc) {

    var doc = doc || document;
    var sidebar = doc.createElement('div');
    sidebar.setAttribute('class', 'sidebar');

    jsonObj = JSON.parse(jsonLinks);

    for (var sectionKey in jsonObj.sections) {

        var sectionObj = jsonObj.sections[sectionKey];
        var sectionDiv = doc.createElement('div');
        sectionDiv.setAttribute('class', 'sidebarSection');
        sectionDiv.textContent = sectionObj.name;
        sidebar.appendChild(sectionDiv);

        //Make sure each section is sorted
        var sectionKeys = [];
        for (var sectionKey in sectionObj.items) {
            sectionKeys.push(sectionObj.items[sectionKey].name);
        }
        sectionKeys.sort();

        for (var itemKey in sectionKeys) {
            var itemObj;
            for (var section in sectionObj.items) {
                if (sectionObj.items[section].name == sectionKeys[itemKey]) {
                    itemObj = sectionObj.items[section];
                    break;
                }
            }
            if (itemObj === null) {
                break;
            }
            
            //Create link using name and link defined in jsonLinks
            var linkDiv = doc.createElement('div');
            var linkElement = doc.createElement('a');
            linkElement.setAttribute('href', itemObj.link);
            linkElement.setAttribute('class', 'sidebarLink');
            linkElement.textContent = itemObj.name;
            linkDiv.appendChild(linkElement);
            sectionDiv.appendChild(linkDiv);
        }

    }

    doc.body.appendChild(sidebar);

}

jsonLinks = `
{
    "sections": {
        "main_pages": {
            "name": "Main Pages",
            "items": [
                {
                    "name": "Introduction",
                    "link": "intro.html"
                },
                {
                    "name": "Developer's Guide",
                    "link": "dev_intro.html"
                }
            ]
        },
        "classes": {
            "name": "Classes",
            "items": [
                {
                    "name": "Geometry Interface",
                    "link": "geometry_interfaces.html",
                    "brief": "Geometry Interfaces provide a graphical interface to Atoms objects and their properties."
                },
                {
                    "name": "Arrow",
                    "link": "arrows.html",
                    "brief": "Arrows link Geometry Interfaces and are the interface to the execution of Tasks."
                },
                {
                    "name": "Atom Checker",
                    "link": "atom_checker.html",
                    "brief": "Provides error checking and feedback on the Atom level."
                },
                {
                    "name": "Residue Checker",
                    "link": "residue_checker.html",
                    "brief": "Provides error checking and feedback on the Residue level."
                },
                {
                    "name": "Tasks",
                    "link": "tasks.html",
                    "brief": "Individual processes that are performed on or using Atoms."
                },
                {
                    "name": "Protonator",
                    "link": "protonator.html",
                    "brief": "Provides methods to add protons to Atoms."
                }
            ]
        },
        "formats": {
            "name": "File Formats",
            "items": [
                {
                    "name": ".xat",
                    "link": "xat.html"
                },
                {
                    "name": ".pdb",
                    "link": "xat.html"
                },
                {
                    "name": ".pqr",
                    "link": "xat.html"
                },
                {
                    "name": ".prm",
                    "link": "xat.html"
                },
                {
                    "name": ".com/.gjf",
                    "link": "xat.html"
                },
                {
                    "name": ".p2n",
                    "link": "xat.html"
                },
                {
                    "name": ".mol2",
                    "link": "xat.html"
                }
            ]

        },
        "fileIO": {
            "name": "File Input/Output (dev)",
            "items": [
                {
                    "name": "File Reader",
                    "link": "xat.html"
                },
                {
                    "name": "File Writer",
                    "link": "xat.html"
                }
            ]
        }
    }
}
`