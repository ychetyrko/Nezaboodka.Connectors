## Installation script parts

NezaboodkaMySQL installation script is decomposed to make it more modifiable.

To assemble installation script use [extended #include file processor](https://github.com/SVss/includer) `process.py` in current directory as follows:

```
	python process.py "install.ptrn.sql" "../install.sql"
```
