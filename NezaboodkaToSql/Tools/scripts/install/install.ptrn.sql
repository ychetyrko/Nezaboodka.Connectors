/* ********************************************

	Create Nezaboodka administrative database
			and nezaboodka users

********************************************* */

CREATE DATABASE `nz_admin_db` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_BIN`;
USE `nz_admin_db`;


/* ********************************************

		Administrative database tables
*/

#include "tables/database list.sql"


/* ********************************************

			Stored procedures
*/

#include "procedures/qexec.sql"

#include "procedures/prepare new database.sql"

/*********************************************
		Alter database list
*/
/*	Protocol for altering database list:

	1. call `before_alter_database_list` to create temporary tables if not created;
	2. fill `db_rem_list` and `db_add_list` tables with database names;
	3. call `alter_database_list`.
*/

#include "procedures/alter database list.sql"


/*********************************************
		Alter database schema
*/
/*	Protocol for altering database schema:

	1. call `before_alter_database_schema`;
	2. fill `type_add_list`, `type_rem_list`, `field_add_list` and `field_rem_list` tables;
	3. call `alter_database_schema`.
*/

#include "procedures/alter database schema.ptrn.sql"


/* ********************************************

		Create Nezaboodka users
			and grant rights for databases

********************************************* */

CREATE USER `nz_admin`@'%' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'%';

-- Localhost user
CREATE USER `nz_admin`@'localhost' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'localhost';

FLUSH PRIVILEGES;
