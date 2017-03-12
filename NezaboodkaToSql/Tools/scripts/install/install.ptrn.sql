/* ********************************************

	Create Nezaboodka administrative database
			and nezaboodka users

********************************************* */

CREATE DATABASE `nz_admin_db` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_BIN`;
USE `nz_admin_db`;

#include "tables/database list.sql"

/* ********************************************

			Stored procedures
*/

#include "procedures/qexec.sql"
#include "procedures/alter database list.sql"
#include "procedures/prepare new database.sql"
