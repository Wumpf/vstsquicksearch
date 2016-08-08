# Vsts Quick Search
Visual Studio Team Services's search functionallity is rather slow if you ever tried to find duplicate Work Items in a large database.
This tool allows you to download all tickets for a given query and search through it it in realtime.

The search looks through all elements of a work item and has been proven responsive with more than 16k items in a query.

[Download latest build](latestbuild.zip)

![Screenshot](screenshot.jpg?raw=true)

## Version History

* 1.1
 *  [#1](/../../issues/1) Download from hierarchical queries works now.
 * [#2](/../../issues/2) Work item values are converted to string ahead of time now. This allows searching for fields like id which are numbers.
 * [#4](/../../issues/4) Search words no longer need to appear in a single field.
 
