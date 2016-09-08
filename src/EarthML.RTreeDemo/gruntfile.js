/*
This file in the main entry point for defining grunt tasks and using grunt plugins.
Click here to learn more. http://go.microsoft.com/fwlink/?LinkID=513275&clcid=0x409
*/
module.exports = function (grunt) {

    grunt.loadNpmTasks('grunt-npmcopy');
    grunt.initConfig({
      
        npmcopy: {
            // Anything can be copied 
            libs: {
                options: {
                    destPrefix: 'wwwroot/libs'
                },
                files: {
                    "signalr/": "signalr/**/*.js",
                    "nprogress/": ["nprogress/nprogress.js", "nprogress/nprogress.css"],
                   

                }
            }
        }
    });
};